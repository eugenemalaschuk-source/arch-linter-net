using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

// The explain-command capability's projection of ExpressionParticipation: adds the owning
// contract's stable ID (ArchitectureViolation.ContractId), which callers need to correlate an
// expression result with the ContractIds already reported on the resolved path, alongside the
// expression data ExpressionParticipation already carries.
// HopSource/HopTarget identify which edge on the resolved path this entry belongs to, so
// the same expression appearing on multiple hops (e.g. A→B and B→C) produces distinct entries
// and callers can attribute each result to its hop.
public sealed record ExplainExpressionParticipation(
    string ContractId,
    string Source,
    string? YamlPath,
    ExpressionParticipationResult Result)
{
    public string? HopSource { get; init; }
    public string? HopTarget { get; init; }
}
