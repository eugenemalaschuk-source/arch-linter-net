namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferenceAllowOnlyPayload(
    IReadOnlyCollection<string> AllowedFrameworkGroups,
    IReadOnlyCollection<FrameworkReferenceEvidence>? Evidence = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new FrameworkReferenceAllowOnlyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, AllowedFrameworkGroups,
            Evidence ?? Array.Empty<FrameworkReferenceEvidence>())
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
