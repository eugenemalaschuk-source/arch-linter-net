namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureExplainOutcome(
    string Source,
    string Target,
    IReadOnlyList<string>? Path,
    IReadOnlyList<string> ContractIds)
{
    // Additive: defaults to empty so every existing caller built against the original 4-parameter
    // constructor is unaffected. Populated only for hops whose contributing violation carries a
    // `when`-bearing selector (see ArchitectureExplainApplicationService).
    public IReadOnlyList<ExplainExpressionParticipation> ExpressionParticipation { get; init; } =
        Array.Empty<ExplainExpressionParticipation>();
}
