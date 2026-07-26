namespace ArchLinterNet.Core.Model;

public sealed record PublicApiSurfacePayload(
    string? UndeclaredApiSignature = null,
    bool? ForbiddenPublicConstant = null,
    string? ApiAssemblyName = null,
    string? ApiVisibility = null,
    string? ApiDeltaKind = null,
    string? PreviousApiSignature = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new PublicApiSurfaceDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            UndeclaredApiSignature = UndeclaredApiSignature,
            ForbiddenPublicConstant = ForbiddenPublicConstant,
            ApiAssemblyName = ApiAssemblyName,
            ApiVisibility = ApiVisibility,
            ApiDeltaKind = ApiDeltaKind,
            PreviousApiSignature = PreviousApiSignature
        };
}
