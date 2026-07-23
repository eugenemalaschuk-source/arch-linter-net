namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferencePayload(
    string ForbiddenFrameworkGroup,
    IReadOnlyCollection<FrameworkReferenceEvidence>? Evidence = null) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new FrameworkReferenceDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, ForbiddenFrameworkGroup,
            Evidence ?? Array.Empty<FrameworkReferenceEvidence>())
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
