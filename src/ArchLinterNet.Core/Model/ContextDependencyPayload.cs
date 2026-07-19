namespace ArchLinterNet.Core.Model;

public sealed record ContextDependencyPayload(
    string? SourceRole = null,
    IReadOnlyDictionary<string, object>? SourceMetadata = null,
    string? TargetRole = null,
    IReadOnlyDictionary<string, object>? TargetMetadata = null,
    string? MatchedSelector = null)
    : IArchitectureDiagnosticPayload
{
    // Non-positional so the CLR constructor signature (used by already-compiled consumers)
    // stays at 5 parameters — adding it as a 6th positional parameter would be a binary
    // breaking change even with a default value. Callers assign via object-initializer syntax.
    public IReadOnlyList<ExpressionParticipation>? WhenExpressions { get; init; }
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ContextDependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            SourceRole = SourceRole,
            SourceMetadata = SourceMetadata,
            TargetRole = TargetRole,
            TargetMetadata = TargetMetadata,
            MatchedSelector = MatchedSelector,
            WhenExpressions = WhenExpressions
        };
}
