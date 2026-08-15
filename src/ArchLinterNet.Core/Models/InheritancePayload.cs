namespace ArchLinterNet.Core.Model;

public sealed record InheritancePayload(
    string? ForbiddenBaseType = null,
    string? InheritanceSourceSurface = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new InheritanceDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            ForbiddenBaseType = ForbiddenBaseType,
            InheritanceSourceSurface = InheritanceSourceSurface
        };
}
