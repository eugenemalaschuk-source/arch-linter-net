using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Builds the stable public finding envelope without inspecting display text.</summary>
public static class ArchitectureFindingMapper
{
    public static ArchitectureFinding FromViolation(ArchitectureViolation violation) =>
        FromViolation(violation, mode: null);

    public static ArchitectureFinding FromViolation(ArchitectureViolation violation, string? mode)
    {
        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
        ArchitectureViolationIdentity identity = violation.Identity ?? BuildIdentity(diagnostic);
        ArchitectureDiagnostic projected = violation.Identities.Count > 1
            ? ProjectDiagnosticForIdentity(diagnostic, identity)
            : diagnostic;
        return Create(projected, identity, mode);
    }

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic) =>
        FromDiagnostic(diagnostic, mode: null);

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic, string? mode) =>
        Create(diagnostic, BuildIdentity(diagnostic), mode);

    public static ArchitectureFinding FromBaseline(BaselineLifecycleEntry lifecycle)
    {
        ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
        string state = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle);
        ArchitectureViolationIdentity identity = entry.Identity ?? BuildBaselineFallbackIdentity(entry);
        var diagnostic = new BaselineLifecycleDiagnostic(
            "baseline",
            entry.ContractId,
            entry.ContractGroup,
            entry.SourceType,
            entry.ForbiddenReference,
            entry.Reason,
            entry.Issue,
            lifecycle.Disposition,
            BaselineEntryLifecycleNames.Suppresses(lifecycle.Lifecycle),
            entry.Identity);
        return Create(diagnostic, identity, mode: null) with { BaselineState = state };
    }

    public static ArchitectureFinding FromPolicyError(
        string message,
        ArchitecturePolicyDiagnostic diagnostic,
        string? category = null)
    {
        var details = new ArchitecturePolicyErrorDiagnostic(
            message,
            diagnostic.Kind,
            category,
            diagnostic.ImportChain)
        {
            PolicyLocation = diagnostic.Location,
            RelatedPolicyLocations = diagnostic.RelatedLocations,
        };
        return FromDiagnostic(details);
    }

    /// <summary>
    /// Maps a batch without recomputing occurrence. Production violations carry identities assigned
    /// live by the execution context before ignore matching; aggregated legacy violations expand to
    /// one normalized finding per authoritative identity.
    /// </summary>
    public static IReadOnlyList<ArchitectureFinding> FromViolations(
        IEnumerable<ArchitectureViolation> violations,
        string? mode = null)
    {
        var findings = new List<ArchitectureFinding>();
        foreach (ArchitectureViolation violation in violations)
        {
            ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
            IReadOnlyCollection<ArchitectureViolationIdentity> identities = violation.Identities.Count > 0
                ? violation.Identities
                : violation.Identity is { } identity
                    ? new[] { identity }
                    : new[] { BuildIdentity(diagnostic) };
            bool isAggregated = identities.Count > 1;
            findings.AddRange(identities.Select(identity => Create(
                isAggregated ? ProjectDiagnosticForIdentity(diagnostic, identity) : diagnostic,
                identity,
                mode)));
        }

        return findings;
    }

    public static IReadOnlyList<ArchitectureFinding> Order(IEnumerable<ArchitectureFinding> findings) =>
        findings.OrderBy(finding => finding.ContractId ?? finding.ContractName, StringComparer.Ordinal)
            .ThenBy(finding => finding.CanonicalIdentity, StringComparer.Ordinal)
            .ThenBy(finding => finding.Kind, StringComparer.Ordinal)
            .ThenBy(finding => finding.SourceLocation?.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.SourceLocation?.Line)
            .ThenBy(finding => finding.SourceLocation?.Column)
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
        ArchitectureDiagnosticKind.Baseline => "baseline",
        ArchitectureDiagnosticKind.ArchitecturePolicyError => "architecture_policy_error",
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
            MessageCode = KindToken(diagnostic.Kind),
            PolicyOrigin = diagnostic.PolicyLocation,
            RelatedPolicyOrigins = diagnostic.RelatedPolicyLocations,
            SourceLocation = SourceLocationOf(diagnostic),
        };

    private static ArchitectureFindingSourceLocation? SourceLocationOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        LayoutConventionDiagnostic { MatchedFilePath: { } path } => new ArchitectureFindingSourceLocation(path),
        FrameworkReferenceDiagnostic { Evidence: { Count: > 0 } evidence } =>
            new ArchitectureFindingSourceLocation(evidence.First().SourcePath),
        FrameworkReferenceAllowOnlyDiagnostic { Evidence: { Count: > 0 } evidence } =>
            new ArchitectureFindingSourceLocation(evidence.First().SourcePath),
        ProjectMetadataDiagnostic { ProjectMetadataSourcePath: { } path } => new ArchitectureFindingSourceLocation(path),
        BuildStatePreflightDiagnostic { Evidence.ProjectPath: var path } => new ArchitectureFindingSourceLocation(path),
        _ => null,
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

    private static ArchitectureViolationIdentity BuildBaselineFallbackIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        string family = ArchitectureViolationIdentity.ResolveContractFamily(entry.ContractGroup);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            family,
            ArchitectureViolationIdentity.ResolveKind(family),
            entry.ContractId,
            null,
            entry.SourceType,
            null,
            null,
            null,
            entry.ForbiddenReference,
            0);
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
            BaselineLifecycleDiagnostic baseline =>
                (null, null, null, null, baseline.ForbiddenReference, baseline.ContractGroup),
            ArchitecturePolicyErrorDiagnostic policyError =>
                (null, PolicyErrorImportPosition(policyError), null, null,
                    policyError.PolicyLocation?.YamlPath ?? policyError.DiagnosticKind.ToString(),
                    PolicyErrorConfiguration(policyError)),
            _ => (null, null, null, null, SourceIdentifier(diagnostic), diagnostic.Kind.ToString()),
        };
    }

    private static ArchitectureDiagnostic ProjectDiagnosticForIdentity(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity)
    {
        return diagnostic switch
        {
            DependencyDiagnostic dependency =>
                dependency with { ForbiddenReferences = ReferencesForIdentity(dependency.ForbiddenReferences, identity) },
            ExternalDependencyDiagnostic external =>
                external with { ForbiddenReferences = ReferencesForIdentity(external.ForbiddenReferences, identity) },
            PackageDependencyDiagnostic package =>
                package with { ForbiddenReferences = ReferencesForIdentity(package.ForbiddenReferences, identity) },
            PackageAllowOnlyDiagnostic package =>
                package with { ForbiddenReferences = ReferencesForIdentity(package.ForbiddenReferences, identity) },
            FrameworkReferenceDiagnostic framework => framework with
            {
                ForbiddenReferences = ReferencesForIdentity(framework.ForbiddenReferences, identity),
                Evidence = FrameworkEvidenceForIdentity(
                    framework.Evidence,
                    framework.ForbiddenReferences,
                    identity),
            },
            FrameworkReferenceAllowOnlyDiagnostic framework => framework with
            {
                ForbiddenReferences = ReferencesForIdentity(framework.ForbiddenReferences, identity),
                Evidence = FrameworkEvidenceForIdentity(
                    framework.Evidence,
                    framework.ForbiddenReferences,
                    identity),
            },
            CompositionDiagnostic composition =>
                composition with { ForbiddenReferences = ReferencesForIdentity(composition.ForbiddenReferences, identity) },
            _ => diagnostic,
        };
    }

    private static IReadOnlyCollection<string> ReferencesForIdentity(
        IReadOnlyCollection<string> references,
        ArchitectureViolationIdentity identity)
    {
        if (identity.TargetMember is not { Length: > 0 } targetMember)
        {
            return references;
        }

        string[] selected = references
            .Where(reference => ReferenceMatchesIdentity(reference, targetMember))
            .ToArray();
        return selected.Length == 0 ? references : selected;
    }

    private static IReadOnlyCollection<FrameworkReferenceEvidence> FrameworkEvidenceForIdentity(
        IReadOnlyCollection<FrameworkReferenceEvidence> evidence,
        IReadOnlyCollection<string> references,
        ArchitectureViolationIdentity identity)
    {
        if (identity.TargetMember is not { Length: > 0 } targetMember)
        {
            return evidence;
        }

        FrameworkReferenceEvidence[] selected = evidence
            .Where(item =>
                string.Equals(item.FrameworkName, targetMember, StringComparison.Ordinal)
                || string.Equals(
                    $"{item.FrameworkName} ({item.TargetFramework})",
                    targetMember,
                    StringComparison.Ordinal))
            .ToArray();
        return references.Any(reference => ReferenceMatchesIdentity(reference, targetMember))
            ? selected
            : evidence;
    }

    private static bool ReferenceMatchesIdentity(string reference, string targetMember) =>
        reference.Equals(targetMember, StringComparison.Ordinal)
        || reference.StartsWith(targetMember + "@", StringComparison.Ordinal)
        || reference.StartsWith(targetMember + " ", StringComparison.Ordinal);

    private static string? PolicyErrorImportPosition(ArchitecturePolicyErrorDiagnostic policyError) =>
        policyError.ImportChain.Count == 0
            ? null
            : $"{policyError.ImportChain.Count - 1}:{policyError.ImportChain[^1]}";

    private static string PolicyErrorConfiguration(ArchitecturePolicyErrorDiagnostic policyError)
    {
        string importChain = string.Join(" -> ", policyError.ImportChain);
        return $"kind={policyError.DiagnosticKind};category={policyError.ErrorCategory ?? "<none>"};"
            + $"import_chain={importChain}";
    }

    private static string SourceTypeOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        CycleDiagnostic cycle => cycle.Path,
        BuildStatePreflightDiagnostic preflight => preflight.Evidence.ProjectPath,
        UnmatchedIgnoreDiagnostic unmatched => unmatched.SourceType,
        PolicyConsistencyDiagnostic policy => policy.RepresentativeType ?? policy.CheckKind,
        BaselineLifecycleDiagnostic baseline => baseline.SourceType,
        ArchitecturePolicyErrorDiagnostic policyError => policyError.PolicyLocation?.SourcePath ?? "<policy>",
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
        BaselineLifecycleDiagnostic d => d.SourceType,
        ArchitecturePolicyErrorDiagnostic d => d.PolicyLocation?.SourcePath ?? "<policy>",
        _ => string.Empty,
    };
}
