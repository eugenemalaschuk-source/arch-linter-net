using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

public interface IArchitectureContractExecutor
{
    ArchitectureContractExecutor.ExecutionResult Execute(
        ArchitectureAnalysisSession session,
        string mode,
        ArchitectureContractHandlerRegistry handlerRegistry,
        bool includeAsmdefContracts = true,
        ValidationTiming? timing = null);
}

public sealed class ArchitectureContractExecutor : IArchitectureContractExecutor
{
    private const string CoverageFamily = "coverage";
    private const string AsmdefFamily = "asmdef";

    public sealed record ExecutionResult(
        IReadOnlyCollection<ArchitectureViolation> Violations,
        IReadOnlyCollection<string> Cycles,
        IReadOnlyCollection<ArchitectureViolation> CoverageViolations,
        IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries);

    public ExecutionResult Execute(
        ArchitectureAnalysisSession session,
        string mode,
        ArchitectureContractHandlerRegistry handlerRegistry,
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
                int coverageCount = 0;
                using (timing?.MeasureContractFamily(CoverageFamily, () => coverageCount))
                {
                    foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, CoverageFamily))
                    {
                        coverageCount++;
                        coverageViolations.AddRange(handlerRegistry.Execute(CoverageFamily, session, contract).Violations);

                        ArchitectureCoverageSummary? summary =
                            session.BuildCoverageSummary((ArchitectureCoverageContract)contract);
                        if (summary != null)
                        {
                            coverageSummaries.Add(summary);
                        }
                    }
                }

                continue;
            }

            if (family == AsmdefFamily && !includeAsmdefContracts)
            {
                continue;
            }

            int count = 0;
            using (timing?.MeasureContractFamily(family, () => count))
            {
                foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, family))
                {
                    count++;
                    ArchitectureHandlerResult result = handlerRegistry.Execute(family, session, contract);
                    violations.AddRange(result.Violations);
                    cycles.AddRange(result.Cycles);
                }
            }
        }

        return new ExecutionResult(violations, cycles, coverageViolations, coverageSummaries);
    }
}
