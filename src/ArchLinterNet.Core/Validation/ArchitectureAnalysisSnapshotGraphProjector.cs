using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureAnalysisSnapshotGraphProjector
{
    internal static ArchitectureGraphOutcome Project(
        ArchitectureAnalysisSession session,
        IArchitectureContractExecutor contractExecutor,
        IArchitectureContractHandlerRegistry handlerRegistry,
        string mode,
        ArchitectureGraphLevel level)
    {
        if (mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        List<ArchitectureViolation> graphViolations = new();
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries =
            Array.Empty<ArchitectureCoverageSummary>();
        graphViolations.AddRange(session.CheckConfiguration(strict: mode != "audit"));
        string[] executionModes = mode == "all" ? ["strict", "audit"] : [mode];
        foreach (string executionMode in executionModes)
        {
            ArchitectureContractExecutionResult execution = contractExecutor.Execute(
                session,
                executionMode,
                handlerRegistry,
                includeAsmdefContracts: false);
            graphViolations.AddRange(execution.Violations);
            coverageSummaries = coverageSummaries.Concat(execution.CoverageSummaries).ToArray();
        }

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            session,
            level,
            graphViolations,
            out IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>> edgeViolations);
        return new ArchitectureGraphOutcome(graph)
        {
            EdgeViolations = edgeViolations,
            CoverageSummaries = coverageSummaries,
            SourceExpansion = session.Document.SourceExpansion,
            SelectorParticipation = session.SubtractiveMatcherParticipation,
        };
    }
}
