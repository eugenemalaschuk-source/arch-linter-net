namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferenceAllowOnlyPayload(IReadOnlyCollection<string> AllowedFrameworkGroups)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new FrameworkReferenceAllowOnlyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, AllowedFrameworkGroups)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
