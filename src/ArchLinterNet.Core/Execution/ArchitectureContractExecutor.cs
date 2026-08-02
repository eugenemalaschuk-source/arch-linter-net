using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureContractExecutor : IArchitectureContractExecutor
{
    private const string CoverageFamily = "coverage";
    private const string AsmdefFamily = "asmdef";

    public ArchitectureContractExecutionResult Execute(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        bool includeAsmdefContracts = true,
        ValidationTiming? timing = null)
    {
        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        session.PrepareRuleInputCoverageDeferral(mode);

        var standardFamilyFindings = new StandardFamilyFindings();
        List<ArchitectureViolation> coverageViolations = new();
        List<ArchitectureCoverageSummary> coverageSummaries = new();
        Dictionary<string, int> resultCounts = new(StringComparer.Ordinal);

        // Iterating the catalog's families rather than a hardcoded per-family list means a new
        // violations-or-cycles-shaped family (added to ArchitectureContractCatalog.Build plus a
        // registered handler) is dispatched here with no executor changes. "coverage" (separate
        // output bucket + summary) and "asmdef" (CLI-controlled inclusion toggle) are genuine
        // runtime decisions, not god-executor ceremony, so they stay as small special cases.
        foreach (string family in session.Catalog.FamiliesInOrder)
        {
            session.Context.CancellationToken.ThrowIfCancellationRequested();

            if (family == CoverageFamily)
            {
                ExecuteCoverageFamily(session, mode, handlerRegistry, timing, coverageViolations, coverageSummaries, resultCounts);
                continue;
            }

            if (family == AsmdefFamily && !includeAsmdefContracts)
            {
                continue;
            }

            ExecuteStandardFamily(session, mode, family, handlerRegistry, timing, standardFamilyFindings, resultCounts);
        }

        return new ArchitectureContractExecutionResult(
            standardFamilyFindings.Violations,
            standardFamilyFindings.Cycles,
            coverageViolations,
            coverageSummaries)
        {
            CycleFindings = standardFamilyFindings.CycleFindings,
            ContractFamilyResultCounts = resultCounts,
        };
    }

    private static void ExecuteCoverageFamily(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        List<ArchitectureViolation> coverageViolations,
        List<ArchitectureCoverageSummary> coverageSummaries,
        IDictionary<string, int> resultCounts)
    {
        int coverageCount = 0;
        using (timing?.MeasureContractFamily(CoverageFamily, () => coverageCount))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, CoverageFamily))
            {
                session.Context.CancellationToken.ThrowIfCancellationRequested();
                coverageCount++;
                int identityCursor = session.FindingIdentityCursor;
                int resultCount = 0;
                IReadOnlyCollection<ArchitectureViolation> violations = session.AttachFindingIdentities(
                        handlerRegistry.Execute(CoverageFamily, session, contract).Violations,
                        identityCursor)
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract))
                    .ToArray();
                coverageViolations.AddRange(violations);
                resultCount += violations.Count;

                ArchitectureCoverageSummary? summary =
                    session.BuildCoverageSummary((ArchitectureCoverageContract)contract);
                if (summary != null)
                {
                    coverageSummaries.Add(summary);
                    resultCount++;
                }

                AddResultCount(resultCounts, CoverageFamily, resultCount);
            }
        }
    }

    private static void ExecuteStandardFamily(
        ArchitectureAnalysisSession session,
        string mode,
        string family,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        StandardFamilyFindings findings,
        IDictionary<string, int> resultCounts)
    {
        int count = 0;
        using (timing?.MeasureContractFamily(family, () => count))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, family))
            {
                session.Context.CancellationToken.ThrowIfCancellationRequested();
                count++;
                int identityCursor = session.FindingIdentityCursor;
                ArchitectureHandlerResult result = handlerRegistry.Execute(family, session, contract);
                IReadOnlyCollection<ArchitectureViolation> violations = session.AttachFindingIdentities(result.Violations, identityCursor)
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract))
                    .ToArray();
                findings.Violations.AddRange(violations);
                string cycleIdPrefix = contract.Id is null ? string.Empty : $"[{contract.Id}] ";
                int cycleCount = 0;
                foreach (string cycle in result.Cycles)
                {
                    findings.Cycles.Add(cycle);
                    cycleCount++;
                    string normalizedPath = cycleIdPrefix.Length > 0 && cycle.StartsWith(cycleIdPrefix, StringComparison.Ordinal)
                        ? cycle[cycleIdPrefix.Length..]
                        : cycle;
                    findings.CycleFindings.Add(session.Document.Provenance.Enrich(
                        new ArchitectureCycleFinding(contract.Name, contract.Id, normalizedPath),
                        contract));
                }

                AddResultCount(resultCounts, family, violations.Count + cycleCount);
            }
        }
    }

    private static void AddResultCount(IDictionary<string, int> resultCounts, string family, int count)
    {
        resultCounts.TryGetValue(family, out int current);
        resultCounts[family] = current + count;
    }

    private sealed class StandardFamilyFindings
    {
        public List<ArchitectureViolation> Violations { get; } = new();

        public List<string> Cycles { get; } = new();

        public List<ArchitectureCycleFinding> CycleFindings { get; } = new();
    }
}
