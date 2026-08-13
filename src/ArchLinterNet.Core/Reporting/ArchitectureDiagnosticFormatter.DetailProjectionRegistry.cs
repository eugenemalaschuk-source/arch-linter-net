using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

internal delegate void DiagnosticDetailProjector(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj);

internal sealed record DiagnosticDetailProjectionEntry(Type DiagnosticType, DiagnosticDetailProjector Projector);

public sealed partial class ArchitectureDiagnosticFormatter
{
    // Single ordered source of truth for every diagnostic kind's structured CI/JSON detail
    // projection. Adding a diagnostic family means appending one entry here that references that
    // family's own Apply*CiFields method, instead of adding a case to a shared switch (see #453).
    // Mirrors ArchitectureContractFamilyRegistry.All / ArchitectureContractHandlerRegistry in
    // src/ArchLinterNet.Core/Execution/: an ordered static list feeding a Dictionary-backed lookup.
    // Nested (rather than a standalone type) so its entries can reference the family projector
    // methods, most of which are private to this partial class, without widening their visibility.
    internal static class DiagnosticDetailProjectionRegistry
    {
        public static IReadOnlyList<DiagnosticDetailProjectionEntry> All { get; } = new List<DiagnosticDetailProjectionEntry>
        {
            new(typeof(DependencyDiagnostic), (d, o) => ApplyDependencyCiFields((DependencyDiagnostic)d, o)),
            new(typeof(ExternalDependencyDiagnostic), (d, o) => ApplyExternalDependencyCiFields((ExternalDependencyDiagnostic)d, o)),
            new(typeof(PackageDependencyDiagnostic), (d, o) => ApplyPackageDependencyCiFields((PackageDependencyDiagnostic)d, o)),
            new(typeof(PackageAllowOnlyDiagnostic), (d, o) => ApplyPackageAllowOnlyCiFields((PackageAllowOnlyDiagnostic)d, o)),
            new(typeof(FrameworkReferenceDiagnostic), (d, o) => ApplyFrameworkReferenceCiFields((FrameworkReferenceDiagnostic)d, o)),
            new(typeof(FrameworkReferenceAllowOnlyDiagnostic), (d, o) => ApplyFrameworkReferenceAllowOnlyCiFields((FrameworkReferenceAllowOnlyDiagnostic)d, o)),
            new(typeof(TypePlacementDiagnostic), (d, o) => ApplyTypePlacementCiFields((TypePlacementDiagnostic)d, o)),
            new(typeof(LayoutConventionDiagnostic), (d, o) => ApplyLayoutConventionCiFields((LayoutConventionDiagnostic)d, o)),
            new(typeof(PublicApiSurfaceDiagnostic), (d, o) => ApplyPublicApiSurfaceCiFields((PublicApiSurfaceDiagnostic)d, o)),
            new(typeof(AttributeUsageDiagnostic), (d, o) => ApplyAttributeUsageCiFields((AttributeUsageDiagnostic)d, o)),
            new(typeof(InheritanceDiagnostic), (d, o) => ApplyInheritanceCiFields((InheritanceDiagnostic)d, o)),
            new(typeof(InterfaceImplementationDiagnostic), (d, o) => ApplyInterfaceImplementationCiFields((InterfaceImplementationDiagnostic)d, o)),
            new(typeof(CompositionDiagnostic), (d, o) => ApplyCompositionCiFields((CompositionDiagnostic)d, o)),
            new(typeof(ProjectMetadataDiagnostic), (d, o) => ApplyProjectMetadataCiFields((ProjectMetadataDiagnostic)d, o)),
            new(typeof(ConfigurationDiagnostic), (d, o) => ApplyConfigurationCiFields((ConfigurationDiagnostic)d, o)),
            new(typeof(ContextDependencyDiagnostic), (d, o) => ApplyContextDependencyCiFields((ContextDependencyDiagnostic)d, o)),
            new(typeof(ContextAllowOnlyDiagnostic), (d, o) => ApplyContextAllowOnlyCiFields((ContextAllowOnlyDiagnostic)d, o)),
            new(typeof(PortBoundaryDiagnostic), (d, o) => ApplyPortBoundaryCiFields((PortBoundaryDiagnostic)d, o)),
            new(typeof(CycleDiagnostic), (d, o) => ApplyCycleCiFields((CycleDiagnostic)d, o)),
            new(typeof(BuildStatePreflightDiagnostic), (d, o) => ApplyBuildStatePreflightCiFields((BuildStatePreflightDiagnostic)d, o)),
            new(typeof(UnmatchedIgnoreDiagnostic), (d, o) => ApplyUnmatchedIgnoreCiFields((UnmatchedIgnoreDiagnostic)d, o)),
            new(typeof(PolicyConsistencyDiagnostic), (d, o) => ApplyPolicyConsistencyCiFields((PolicyConsistencyDiagnostic)d, o)),
            new(typeof(BaselineLifecycleDiagnostic), (d, o) => ApplyBaselineLifecycleCiFields((BaselineLifecycleDiagnostic)d, o)),
            new(typeof(ArchitecturePolicyErrorDiagnostic), (d, o) => ApplyArchitecturePolicyErrorCiFields((ArchitecturePolicyErrorDiagnostic)d, o)),
        };

        public static IReadOnlyDictionary<Type, DiagnosticDetailProjector> ByType { get; } =
            All.ToDictionary(entry => entry.DiagnosticType, entry => entry.Projector);
    }
}
