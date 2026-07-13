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

        List<ArchitectureViolation> violations = new();
        List<string> cycles = new();
        List<ArchitectureViolation> coverageViolations = new();
        List<ArchitectureCoverageSummary> coverageSummaries = new();

        // Iterating the catalog's families rather than a hardcoded per-family list means a new
        // violations-or-cycles-shaped family (added to ArchitectureContractCatalog.Build plus a
        // registered handler) is dispatched here with no executor changes. "coverage" (separate
        // output bucket + summary) and "asmdef" (CLI-controlled inclusion toggle) are genuine
        // runtime decisions, not god-executor ceremony, so they stay as small special cases.
        foreach (string family in session.Catalog.FamiliesInOrder)
        {
            if (family == CoverageFamily)
            {
                ExecuteCoverageFamily(session, mode, handlerRegistry, timing, coverageViolations, coverageSummaries);
                continue;
            }

            if (family == AsmdefFamily && !includeAsmdefContracts)
            {
                continue;
            }

            ExecuteStandardFamily(session, mode, family, handlerRegistry, timing, violations, cycles);
        }

        return new ArchitectureContractExecutionResult(violations, cycles, coverageViolations, coverageSummaries);
    }

    private static void ExecuteCoverageFamily(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        List<ArchitectureViolation> coverageViolations,
        List<ArchitectureCoverageSummary> coverageSummaries)
    {
        int coverageCount = 0;
        using (timing?.MeasureContractFamily(CoverageFamily, () => coverageCount))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, CoverageFamily))
            {
                coverageCount++;
                coverageViolations.AddRange(handlerRegistry.Execute(CoverageFamily, session, contract).Violations
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract)));

                ArchitectureCoverageSummary? summary =
                    session.BuildCoverageSummary((ArchitectureCoverageContract)contract);
                if (summary != null)
                {
                    coverageSummaries.Add(summary);
                }
            }
        }
    }

    private static void ExecuteStandardFamily(
        ArchitectureAnalysisSession session,
        string mode,
        string family,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        List<ArchitectureViolation> violations,
        List<string> cycles)
    {
        int count = 0;
        using (timing?.MeasureContractFamily(family, () => count))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, family))
            {
                count++;
                ArchitectureHandlerResult result = handlerRegistry.Execute(family, session, contract);
                violations.AddRange(result.Violations
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract)));
                cycles.AddRange(result.Cycles);
            }
        }
    }
}
