namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferencePayload(string ForbiddenFrameworkGroup) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new FrameworkReferenceDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, ForbiddenFrameworkGroup)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
