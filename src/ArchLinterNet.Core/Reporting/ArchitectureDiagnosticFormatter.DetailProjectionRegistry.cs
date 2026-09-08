using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

internal delegate void DiagnosticDetailProjector(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj);

internal sealed record DiagnosticDetailProjectionEntry(Type DiagnosticType, DiagnosticDetailProjector Projector);

internal static class ArchitectureDiagnosticDetailProjectionRegistry
{
    // Single ordered source of truth for every diagnostic kind's structured CI/JSON detail
    // projection. Adding a diagnostic family means appending one entry here that references that
    // family's own Apply*CiFields method, instead of adding a case to a shared switch (see #453).
    // Mirrors ArchitectureContractFamilyRegistry.All / ArchitectureContractHandlerRegistry in
    // src/ArchLinterNet.Core/Execution/: an ordered static list feeding a Dictionary-backed lookup.
    public static IReadOnlyList<DiagnosticDetailProjectionEntry> All { get; } = new List<DiagnosticDetailProjectionEntry>
        {
            new(typeof(DependencyDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyDependencyCiFields((DependencyDiagnostic)d, o)),
            new(typeof(ExternalDependencyDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyExternalDependencyCiFields((ExternalDependencyDiagnostic)d, o)),
            new(typeof(PackageDependencyDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyPackageDependencyCiFields((PackageDependencyDiagnostic)d, o)),
            new(typeof(PackageAllowOnlyDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyPackageAllowOnlyCiFields((PackageAllowOnlyDiagnostic)d, o)),
            new(typeof(MetricBudgetDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyMetricBudgetCiFields((MetricBudgetDiagnostic)d, o)),
            new(typeof(FrameworkReferenceDiagnostic), (d, o) => ArchitectureFrameworkReferenceRenderer.ApplyFrameworkReferenceCiFields((FrameworkReferenceDiagnostic)d, o)),
            new(typeof(FrameworkReferenceAllowOnlyDiagnostic), (d, o) => ArchitectureFrameworkReferenceRenderer.ApplyFrameworkReferenceAllowOnlyCiFields((FrameworkReferenceAllowOnlyDiagnostic)d, o)),
            new(typeof(TypePlacementDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyTypePlacementCiFields((TypePlacementDiagnostic)d, o)),
            new(typeof(LayoutConventionDiagnostic), (d, o) => ArchitectureLayoutConventionRenderer.ApplyLayoutConventionCiFields((LayoutConventionDiagnostic)d, o)),
            new(typeof(PublicApiSurfaceDiagnostic), (d, o) => ArchitecturePublicApiSurfaceRenderer.ApplyPublicApiSurfaceCiFields((PublicApiSurfaceDiagnostic)d, o)),
            new(typeof(ContractSurfaceExposureDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyContractSurfaceExposureCiFields((ContractSurfaceExposureDiagnostic)d, o)),
            new(typeof(AttributeUsageDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyAttributeUsageCiFields((AttributeUsageDiagnostic)d, o)),
            new(typeof(InheritanceDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyInheritanceCiFields((InheritanceDiagnostic)d, o)),
            new(typeof(InterfaceImplementationDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyInterfaceImplementationCiFields((InterfaceImplementationDiagnostic)d, o)),
            new(typeof(CompositionDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyCompositionCiFields((CompositionDiagnostic)d, o)),
            new(typeof(ProjectMetadataDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyProjectMetadataCiFields((ProjectMetadataDiagnostic)d, o)),
            new(typeof(ConfigurationDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyConfigurationCiFields((ConfigurationDiagnostic)d, o)),
            new(typeof(ContextDependencyDiagnostic), (d, o) => ArchitectureDiagnosticContextRenderer.ApplyContextDependencyCiFields((ContextDependencyDiagnostic)d, o)),
            new(typeof(ContextAllowOnlyDiagnostic), (d, o) => ArchitectureDiagnosticContextRenderer.ApplyContextAllowOnlyCiFields((ContextAllowOnlyDiagnostic)d, o)),
            new(typeof(PortBoundaryDiagnostic), (d, o) => ArchitectureDiagnosticContextRenderer.ApplyPortBoundaryCiFields((PortBoundaryDiagnostic)d, o)),
            new(typeof(CycleDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyCycleCiFields((CycleDiagnostic)d, o)),
            new(typeof(BuildStatePreflightDiagnostic), (d, o) => ArchitectureBuildStatePreflightRenderer.ApplyBuildStatePreflightCiFields((BuildStatePreflightDiagnostic)d, o)),
            new(typeof(UnmatchedIgnoreDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyUnmatchedIgnoreCiFields((UnmatchedIgnoreDiagnostic)d, o)),
            new(typeof(PolicyConsistencyDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyPolicyConsistencyCiFields((PolicyConsistencyDiagnostic)d, o)),
            new(typeof(BaselineLifecycleDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyBaselineLifecycleCiFields((BaselineLifecycleDiagnostic)d, o)),
            new(typeof(ArchitecturePolicyErrorDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyArchitecturePolicyErrorCiFields((ArchitecturePolicyErrorDiagnostic)d, o)),
            new(typeof(ArchitectureApplicabilityDiagnostic), (d, o) => ArchitectureNormalizedDetailsProjector.ApplyArchitectureApplicabilityCiFields((ArchitectureApplicabilityDiagnostic)d, o)),
            new(typeof(ImportedExternalDiagnostic), (d, o) => ArchitectureDiagnosticFormatter.ApplyImportedExternalDiagnosticCiFields((ImportedExternalDiagnostic)d, o)),
    };

    public static IReadOnlyDictionary<Type, DiagnosticDetailProjector> ByType { get; } =
        All.ToDictionary(entry => entry.DiagnosticType, entry => entry.Projector);
}
