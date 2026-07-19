using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

// The explain-command capability's projection of ExpressionParticipation: adds the owning
// contract's stable ID (ArchitectureViolation.ContractId), which callers need to correlate an
// expression result with the ContractIds already reported on the resolved path, alongside the
// expression data ExpressionParticipation already carries.
public sealed record ExplainExpressionParticipation(
    string ContractId,
    string Source,
    string? YamlPath,
    ExpressionParticipationResult Result);
