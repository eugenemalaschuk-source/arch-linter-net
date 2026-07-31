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

    // Concrete graph construction executes type/layout contracts as part of the same pass. Carry
    // their typed selector evidence into explain rather than asking its formatter to infer scope
    // from diagnostics or rerun matchers.
    internal IReadOnlyList<Model.ArchitectureSubtractiveMatcherParticipation>? SelectorParticipation { get; init; }
}
