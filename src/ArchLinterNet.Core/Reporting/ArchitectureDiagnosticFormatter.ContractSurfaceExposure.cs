using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static string FormatContractSurfaceExposureContextForHumans(
        ContractSurfaceExposureDiagnostic exposure)
    {
        string site = exposure.MemberOrMetadataSite is { Length: > 0 }
            ? $", site: {exposure.MemberOrMetadataSite}"
            : string.Empty;
        string reviewedSurface = exposure.ReviewedPublicApiSurface is { Length: > 0 }
            ? $", reviewed_public_api_surface: {exposure.ReviewedPublicApiSurface}"
            : string.Empty;
        return $" (source_assembly: {exposure.SourceAssemblyName}, "
            + $"source_surface: {exposure.SourceSurface}, "
            + $"declaring_source_type: {exposure.DeclaringSourceType ?? exposure.SourceType}, "
            + $"exposure_path: {exposure.ExposurePath}, "
            + $"canonical_exposure_path: {exposure.CanonicalExposurePath}, "
            + $"target_assembly: {exposure.TargetAssemblyName}, "
            + $"target_type: {exposure.TargetTypeName}{site}{reviewedSurface})";
    }
}
