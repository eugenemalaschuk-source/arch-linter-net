namespace ArchLinterNet.Core.Model;

/// <summary>Typed evidence for one recursive visible-contract exposure occurrence.</summary>
public sealed record ContractSurfaceExposurePayload(
    string SourceAssemblyName,
    string DeclaringSourceType,
    string ExposurePath,
    string CanonicalExposurePath,
    string TargetAssemblyName,
    string TargetTypeName,
    string SourceSurface,
    string? MemberOrMetadataSite = null,
    string? ReviewedPublicApiSurface = null,
    IReadOnlyCollection<int>? MatchingForbiddenSelectors = null) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ContractSurfaceExposureDiagnostic(
            violation.ContractName,
            violation.ContractId,
            violation.SourceType,
            violation.ForbiddenNamespace,
            violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            SourceAssemblyName = SourceAssemblyName,
            DeclaringSourceType = DeclaringSourceType,
            ExposurePath = ExposurePath,
            CanonicalExposurePath = CanonicalExposurePath,
            TargetAssemblyName = TargetAssemblyName,
            TargetTypeName = TargetTypeName,
            SourceSurface = SourceSurface,
            MemberOrMetadataSite = MemberOrMetadataSite,
            ReviewedPublicApiSurface = ReviewedPublicApiSurface,
            MatchingForbiddenSelectors = MatchingForbiddenSelectors,
        };
}
