namespace ArchLinterNet.Core.Model;

public sealed record ContextAllowOnlyPayload(
    string? SourceRole = null,
    IReadOnlyDictionary<string, object>? SourceMetadata = null,
    string? TargetRole = null,
    IReadOnlyDictionary<string, object>? TargetMetadata = null,
    string? MatchedSelector = null,
    ExpressionParticipation? WhenExpression = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ContextAllowOnlyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            SourceRole = SourceRole,
            SourceMetadata = SourceMetadata,
            TargetRole = TargetRole,
            TargetMetadata = TargetMetadata,
            MatchedSelector = MatchedSelector,
            WhenExpression = WhenExpression
        };
}
