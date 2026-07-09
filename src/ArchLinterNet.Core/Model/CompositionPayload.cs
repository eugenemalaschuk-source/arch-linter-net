namespace ArchLinterNet.Core.Model;

public sealed record CompositionPayload(
    string? SourceMember = null,
    string? MatchedForbiddenApi = null,
    string? ExpectedCompositionBoundary = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new CompositionDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            SourceMember = SourceMember,
            MatchedForbiddenApi = MatchedForbiddenApi,
            ExpectedCompositionBoundary = ExpectedCompositionBoundary
        };
}
