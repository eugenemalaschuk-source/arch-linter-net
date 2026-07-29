using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Builds the stable public finding envelope without inspecting display text.</summary>
public static class ArchitectureFindingMapper
{
    public static ArchitectureFinding FromViolation(ArchitectureViolation violation) =>
        FromViolation(violation, mode: null);

    public static ArchitectureFinding FromViolation(ArchitectureViolation violation, string? mode) =>
        Create(
            ArchitectureDiagnosticMapper.FromViolation(violation),
            violation.Identity ?? BuildIdentity(ArchitectureDiagnosticMapper.FromViolation(violation)),
            mode);

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic) =>
        FromDiagnostic(diagnostic, mode: null);

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic, string? mode) =>
        Create(diagnostic, BuildIdentity(diagnostic), mode);

    /// <summary>
    /// Maps a batch in deterministic producer order and assigns the identity occurrence from the
    /// same zero-occurrence identity used by baseline matching. This keeps repeated calls distinct
    /// without putting a source line number into a stable identity.
    /// </summary>
    public static IReadOnlyList<ArchitectureFinding> FromViolations(
        IEnumerable<ArchitectureViolation> violations,
        string? mode = null)
    {
        var occurrences = new Dictionary<ArchitectureViolationIdentity, int>();
        var findings = new List<ArchitectureFinding>();
        foreach (ArchitectureViolation violation in violations)
        {
            ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
            ArchitectureViolationIdentity identity = violation.Identity ?? BuildIdentity(diagnostic);
            ArchitectureViolationIdentity zeroed = identity with { Occurrence = 0 };
            int occurrence = occurrences.TryGetValue(zeroed, out int count) ? count : 0;
            occurrences[zeroed] = occurrence + 1;
            findings.Add(Create(diagnostic, identity with { Occurrence = occurrence }, mode));
        }

        return findings;
    }

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

    private static ArchitectureFinding Create(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        string? mode) =>
        new(
            ArchitectureFinding.CurrentSchemaVersion,
            KindToken(diagnostic.Kind),
            diagnostic.ContractName,
            diagnostic.ContractId,
            ArchitectureViolationIdentityJson.Serialize(identity),
            diagnostic)
        {
            Identity = identity,
            Mode = mode,
            Severity = mode is null ? null : mode == "strict" ? "error" : "warning",
        };

    private static ArchitectureViolationIdentity BuildIdentity(ArchitectureDiagnostic diagnostic)
    {
        (string? sourceAssembly, string? sourceMember, string? targetAssembly, string? targetType, string? targetMember,
            string? configuration) = IdentityParts(diagnostic);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            KindToken(diagnostic.Kind),
            ArchitectureViolationIdentity.ResolveKind(KindToken(diagnostic.Kind)),
            diagnostic.ContractId ?? diagnostic.ContractName,
            sourceAssembly,
            SourceTypeOf(diagnostic),
            sourceMember,
            targetAssembly,
            targetType,
            targetMember,
            0,
            configuration);
    }

    private static (string? SourceAssembly, string? SourceMember, string? TargetAssembly, string? TargetType,
        string? TargetMember, string? Configuration) IdentityParts(ArchitectureDiagnostic diagnostic)
    {
        return diagnostic switch
        {
            CompositionDiagnostic composition => (
                composition.SourceAssembly, composition.SourceMember, null, null,
                composition.MatchedForbiddenApi ?? string.Join("|", composition.ForbiddenReferences),
                composition.ExpectedCompositionBoundary),
            FrameworkReferenceDiagnostic framework => (
                null, null, null, null, string.Join("|", framework.ForbiddenReferences), framework.ForbiddenFrameworkGroup),
            FrameworkReferenceAllowOnlyDiagnostic framework => (
                null, null, null, null, string.Join("|", framework.ForbiddenReferences),
                string.Join("|", framework.AllowedFrameworkGroups)),
            PackageDependencyDiagnostic package => (
                null, null, null, null, string.Join("|", package.ForbiddenReferences), package.ForbiddenPackageGroup),
            PackageAllowOnlyDiagnostic package => (
                null, null, null, null, string.Join("|", package.ForbiddenReferences),
                string.Join("|", package.AllowedPackageGroups)),
            CycleDiagnostic cycle => (null, null, null, null, cycle.Path, null),
            BuildStatePreflightDiagnostic preflight =>
                (null, null, null, null, preflight.Evidence.ProjectPath, preflight.State.ToString()),
            UnmatchedIgnoreDiagnostic unmatched =>
                (null, null, null, null, unmatched.ForbiddenReference, unmatched.IgnoreIndex.ToString()),
            PolicyConsistencyDiagnostic policy =>
                (null, null, null, null, policy.RepresentativeType ?? policy.CheckKind, policy.CheckKind),
            _ => (null, null, null, null, SourceIdentifier(diagnostic), diagnostic.Kind.ToString()),
        };
    }

    private static string SourceTypeOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        CycleDiagnostic cycle => cycle.Path,
        BuildStatePreflightDiagnostic preflight => preflight.Evidence.ProjectPath,
        UnmatchedIgnoreDiagnostic unmatched => unmatched.SourceType,
        PolicyConsistencyDiagnostic policy => policy.RepresentativeType ?? policy.CheckKind,
        _ => SourceIdentifier(diagnostic),
    };

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
