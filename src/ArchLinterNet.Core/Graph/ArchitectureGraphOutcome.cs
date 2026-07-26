using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureGraphOutcome(ArchitectureDependencyGraph Graph)
{
    // Populated by ArchitectureGraphApplicationService when the richer Build overload runs;
    // null for any alternative IArchitectureGraphApplicationService implementation that does
    // not opt in. ArchitectureExplainApplicationService treats null as "no CEL participation
    // available" and returns an empty ExpressionParticipation list — correct behaviour since
    // only the concrete service runs a real contract-execution pass.
    internal IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>>? EdgeViolations { get; init; }

    internal IReadOnlyCollection<ArchitectureCoverageSummary>? CoverageSummaries { get; init; }

    internal Model.ArchitectureSourceExpansionInventory? SourceExpansion { get; init; }
}
