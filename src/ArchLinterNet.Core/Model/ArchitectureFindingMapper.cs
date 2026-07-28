namespace ArchLinterNet.Core.Model;

/// <summary>Builds the stable public finding envelope without inspecting display text.</summary>
public static class ArchitectureFindingMapper
{
    public static ArchitectureFinding FromViolation(ArchitectureViolation violation, string? mode = null) =>
        FromDiagnostic(ArchitectureDiagnosticMapper.FromViolation(violation), mode);

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic, string? mode = null) =>
        new(
            ArchitectureFinding.CurrentSchemaVersion,
            KindToken(diagnostic.Kind),
            diagnostic.ContractName,
            diagnostic.ContractId,
            CanonicalIdentity(diagnostic),
            diagnostic)
        {
            Mode = mode,
            Severity = mode is null ? null : mode == "strict" ? "error" : "warning",
        };

    public static IReadOnlyList<ArchitectureFinding> Order(IEnumerable<ArchitectureFinding> findings) =>
        findings.OrderBy(finding => finding.ContractId ?? finding.ContractName, StringComparer.Ordinal)
            .ThenBy(finding => finding.CanonicalIdentity, StringComparer.Ordinal)
            .ThenBy(finding => finding.Kind, StringComparer.Ordinal)
            .ToArray();

    public static string KindToken(ArchitectureDiagnosticKind kind) => kind switch
    {
        ArchitectureDiagnosticKind.ExternalDependency => "external_dependency",
        ArchitectureDiagnosticKind.PackageDependency => "package_dependency",
        ArchitectureDiagnosticKind.TypePlacement => "type_placement",
        ArchitectureDiagnosticKind.PublicApiSurface => "public_api_surface",
        ArchitectureDiagnosticKind.AttributeUsage => "attribute_usage",
        ArchitectureDiagnosticKind.InterfaceImplementation => "interface_implementation",
        ArchitectureDiagnosticKind.ProjectMetadata => "project_metadata",
        ArchitectureDiagnosticKind.ContextDependency => "context_dependency",
        ArchitectureDiagnosticKind.ContextAllowOnly => "context_allow_only",
        ArchitectureDiagnosticKind.PortBoundary => "port_boundary",
        ArchitectureDiagnosticKind.LayoutConvention => "layout_convention",
        ArchitectureDiagnosticKind.PackageAllowOnly => "package_allow_only",
        ArchitectureDiagnosticKind.FrameworkReference => "framework_reference",
        ArchitectureDiagnosticKind.FrameworkReferenceAllowOnly => "framework_reference_allow_only",
        ArchitectureDiagnosticKind.BuildStatePreflight => "build_state_preflight",
        ArchitectureDiagnosticKind.PolicyConsistency => "policy_consistency",
        ArchitectureDiagnosticKind.UnmatchedIgnore => "unmatched_ignore",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static string CanonicalIdentity(ArchitectureDiagnostic diagnostic) =>
        string.Join(":", diagnostic.ContractId ?? diagnostic.ContractName, KindToken(diagnostic.Kind), SourceIdentifier(diagnostic));

    private static string SourceIdentifier(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic d => d.SourceType,
        ConfigurationDiagnostic d => d.SourceType,
        ExternalDependencyDiagnostic d => d.SourceType,
        PackageDependencyDiagnostic d => d.SourceType,
        PackageAllowOnlyDiagnostic d => d.SourceType,
        FrameworkReferenceDiagnostic d => d.SourceType,
        FrameworkReferenceAllowOnlyDiagnostic d => d.SourceType,
        TypePlacementDiagnostic d => d.SourceType,
        LayoutConventionDiagnostic d => d.SourceType,
        PublicApiSurfaceDiagnostic d => d.SourceType,
        AttributeUsageDiagnostic d => d.SourceType,
        InheritanceDiagnostic d => d.SourceType,
        InterfaceImplementationDiagnostic d => d.SourceType,
        CompositionDiagnostic d => $"{d.SourceAssembly}:{d.SourceType}:{d.SourceMember}",
        ProjectMetadataDiagnostic d => d.SourceType,
        ContextDependencyDiagnostic d => d.SourceType,
        ContextAllowOnlyDiagnostic d => d.SourceType,
        PortBoundaryDiagnostic d => d.SourceType,
        CycleDiagnostic d => d.Path,
        UnmatchedIgnoreDiagnostic d => $"{d.SourceType}:{d.IgnoreIndex}:{d.ForbiddenReference}",
        PolicyConsistencyDiagnostic d => $"{d.CheckKind}:{d.RepresentativeType}",
        BuildStatePreflightDiagnostic d => $"{d.State}:{d.Evidence.ProjectPath}",
        _ => string.Empty,
    };
}
