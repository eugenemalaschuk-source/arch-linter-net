using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    /// <summary>
    /// Projects a graph view from this snapshot's retained analysis session. Graph's historical
    /// topology contract execution remains its own projection (including its deliberate
    /// includeAsmdefContracts=false choice), but it now runs against the already prepared runner
    /// and loaded fact/session state. This is intentionally internal: graph output is a consumer
    /// projection, while the snapshot remains the sole owner of the immutable analysis lifecycle
    /// and its runner context.
    /// </summary>
    internal ArchitectureGraphOutcome BuildGraph(
        string mode,
        ArchitectureGraphLevel level)
    {
        if (mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_preflight.Blocked)
            {
                throw new InvalidOperationException(
                    "Graph projection cannot materialize a session after build-state preflight was blocked.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            ArchitectureAnalysisSession session = EnsureSetup().Runner.Session;
            List<ArchitectureViolation> graphViolations = new();
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries =
                Array.Empty<ArchitectureCoverageSummary>();
            graphViolations.AddRange(session.CheckConfiguration(strict: mode != "audit"));
            string[] executionModes = mode == "all" ? ["strict", "audit"] : [mode];
            foreach (string executionMode in executionModes)
            {
                ArchitectureContractExecutionResult execution = _contractExecutor.Execute(
                    session,
                    executionMode,
                    _handlerRegistry,
                    includeAsmdefContracts: false);
                graphViolations.AddRange(execution.Violations);
                coverageSummaries = coverageSummaries.Concat(execution.CoverageSummaries).ToArray();
            }

            ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
                session,
                level,
                graphViolations,
                out IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>> edgeViolations);
            _cancellationToken.ThrowIfCancellationRequested();

            return new ArchitectureGraphOutcome(graph)
            {
                EdgeViolations = edgeViolations,
                CoverageSummaries = coverageSummaries,
                SourceExpansion = session.Document.SourceExpansion,
                SelectorParticipation = session.SubtractiveMatcherParticipation,
            };
        }
    }
}
