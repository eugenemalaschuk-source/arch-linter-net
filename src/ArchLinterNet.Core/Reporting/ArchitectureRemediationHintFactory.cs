using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

internal delegate ArchitectureRemediationHint? ArchitectureRemediationHintProvider(
    ArchitectureDiagnostic diagnostic,
    ArchitectureViolationIdentity identity);

internal sealed record ArchitectureRemediationHintProviderEntry(
    Type DiagnosticType,
    ArchitectureRemediationHintProvider Provider);

/// <summary>
/// Exact-type provider registry for optional remediation guidance. It intentionally mirrors the
/// diagnostic detail projection registry: every supported diagnostic type is registered once,
/// while each provider can deliberately return no hint when its evidence is insufficient.
/// </summary>
internal static class ArchitectureRemediationHintProviderRegistry
{
    internal static IReadOnlyList<ArchitectureRemediationHintProviderEntry> All { get; } =
        new List<ArchitectureRemediationHintProviderEntry>
        {
            new(typeof(DependencyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForDependency((DependencyDiagnostic)d, i)),
            new(typeof(ExternalDependencyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForExternalDependency((ExternalDependencyDiagnostic)d, i)),
            new(typeof(PackageDependencyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPackageDependency((PackageDependencyDiagnostic)d, i)),
            new(typeof(PackageAllowOnlyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPackageAllowOnly((PackageAllowOnlyDiagnostic)d, i)),
            new(typeof(FrameworkReferenceDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForFrameworkDependency((FrameworkReferenceDiagnostic)d, i)),
            new(typeof(FrameworkReferenceAllowOnlyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForFrameworkAllowOnly((FrameworkReferenceAllowOnlyDiagnostic)d, i)),
            new(typeof(TypePlacementDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForTypePlacement((TypePlacementDiagnostic)d, i)),
            new(typeof(LayoutConventionDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForLayoutConvention((LayoutConventionDiagnostic)d, i)),
            new(typeof(PublicApiSurfaceDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPublicApiSurface((PublicApiSurfaceDiagnostic)d, i)),
            new(typeof(AttributeUsageDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForAttributeUsage((AttributeUsageDiagnostic)d, i)),
            new(typeof(InheritanceDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForInheritance((InheritanceDiagnostic)d, i)),
            new(typeof(InterfaceImplementationDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForInterfaceImplementation((InterfaceImplementationDiagnostic)d, i)),
            new(typeof(CompositionDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForComposition((CompositionDiagnostic)d, i)),
            new(typeof(ProjectMetadataDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForProjectMetadata((ProjectMetadataDiagnostic)d, i)),
            new(typeof(ConfigurationDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForConfiguration((ConfigurationDiagnostic)d, i)),
            new(typeof(ContextDependencyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForContextDependency((ContextDependencyDiagnostic)d, i)),
            new(typeof(ContextAllowOnlyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForContextAllowOnly((ContextAllowOnlyDiagnostic)d, i)),
            new(typeof(PortBoundaryDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPortBoundary((PortBoundaryDiagnostic)d, i)),
            new(typeof(CycleDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForCycle((CycleDiagnostic)d, i)),
            new(typeof(BuildStatePreflightDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForBuildStatePreflight((BuildStatePreflightDiagnostic)d, i)),
            new(typeof(UnmatchedIgnoreDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForUnmatchedIgnore((UnmatchedIgnoreDiagnostic)d, i)),
            new(typeof(PolicyConsistencyDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPolicyConsistency((PolicyConsistencyDiagnostic)d, i)),
            new(typeof(BaselineLifecycleDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForBaseline((BaselineLifecycleDiagnostic)d, i)),
            new(typeof(ArchitecturePolicyErrorDiagnostic), (d, i) => ArchitectureRemediationHintFactory.ForPolicyError((ArchitecturePolicyErrorDiagnostic)d, i)),
        };

    internal static IReadOnlyDictionary<Type, ArchitectureRemediationHintProvider> ByType { get; } =
        All.ToDictionary(entry => entry.DiagnosticType, entry => entry.Provider);
}

internal static class ArchitectureRemediationHintFactory
{
    private const string NoApprovedSeamCaveat =
        "No approved alternative seam is evidenced; do not broaden policy inputs to accept this finding.";

    internal static ArchitectureRemediationHint? Create(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity)
    {
        if (!ArchitectureRemediationHintProviderRegistry.ByType.TryGetValue(diagnostic.GetType(), out ArchitectureRemediationHintProvider? provider))
        {
            throw new InvalidOperationException(
                $"No remediation-hint provider registered for diagnostic type '{diagnostic.GetType().Name}'.");
        }

        return provider(diagnostic, identity);
    }

    internal static ArchitectureRemediationHint? ForDependency(DependencyDiagnostic diagnostic, ArchitectureViolationIdentity identity)
    {
        if (IsCoverageDiagnostic(diagnostic.ForbiddenNamespace))
        {
            return Create(
                ArchitectureRemediationHintCategory.FixPolicyInput,
                "Add or correct the required architecture coverage input for the affected subject.",
                diagnostic,
                identity,
                Evidence("coverage_gap", diagnostic.ForbiddenNamespace),
                Evidence("affected_subject", diagnostic.SourceType),
                caveat: "Preserve governed scope; do not resolve coverage by excluding the subject broadly.");
        }

        if (diagnostic.AllowedImporters is { Count: > 0 } allowedImporters)
        {
            string seam = string.Join(", ", allowedImporters.OrderBy(name => name, StringComparer.Ordinal));
            return Create(
                ArchitectureRemediationHintCategory.MoveCode,
                "Move the access into an already-declared allowed importer.",
                diagnostic,
                identity,
                Evidence("allowed_importers", seam),
                expectedSeamOrDirection: seam);
        }

        return Review(diagnostic, identity);
    }

    internal static ArchitectureRemediationHint ForExternalDependency(
        ExternalDependencyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => RemoveOrReplace(
        diagnostic, identity, "external_group", diagnostic.ForbiddenExternalGroup);

    internal static ArchitectureRemediationHint ForPackageDependency(
        PackageDependencyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => RemoveOrReplace(
        diagnostic, identity, "package_group", diagnostic.ForbiddenPackageGroup);

    internal static ArchitectureRemediationHint ForPackageAllowOnly(
        PackageAllowOnlyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => RemoveOrReplace(
        diagnostic, identity, "allowed_package_groups", string.Join(", ", diagnostic.AllowedPackageGroups));

    internal static ArchitectureRemediationHint ForFrameworkDependency(
        FrameworkReferenceDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => RemoveOrReplace(
        diagnostic, identity, "framework_group", diagnostic.ForbiddenFrameworkGroup);

    internal static ArchitectureRemediationHint ForFrameworkAllowOnly(
        FrameworkReferenceAllowOnlyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => RemoveOrReplace(
        diagnostic, identity, "allowed_framework_groups", string.Join(", ", diagnostic.AllowedFrameworkGroups));

    internal static ArchitectureRemediationHint ForTypePlacement(
        TypePlacementDiagnostic diagnostic,
        ArchitectureViolationIdentity identity)
    {
        string? expected = diagnostic.ExpectedTypeLocation ?? diagnostic.ExpectedTypeName;
        return expected is null
            ? Review(diagnostic, identity)
            : Create(
                ArchitectureRemediationHintCategory.MoveCode,
                "Move or rename the type to the policy-declared owner.",
                diagnostic,
                identity,
                Evidence("expected_location_or_name", expected),
                expectedSeamOrDirection: expected);
    }

    internal static ArchitectureRemediationHint ForLayoutConvention(
        LayoutConventionDiagnostic diagnostic,
        ArchitectureViolationIdentity identity)
    {
        if (diagnostic.DataUnavailable)
        {
            return Create(
                ArchitectureRemediationHintCategory.FixPolicyInput,
                "Restore the required layout/classification evidence before changing application structure.",
                diagnostic,
                identity,
                Evidence("layout_evidence", "unavailable"));
        }

        string? expected = diagnostic.ExpectedTypeName ?? diagnostic.ExpectedCounterpartName;
        if (expected is not null)
        {
            return Create(
                ArchitectureRemediationHintCategory.MoveCode,
                "Move or rename the declaration to the policy-declared layout owner.",
                diagnostic,
                identity,
                Evidence("expected_name", expected),
                expectedSeamOrDirection: expected);
        }

        if (diagnostic.ExpectedRoles is { Count: > 0 } roles)
        {
            string expectedRoles = string.Join(", ", roles.OrderBy(role => role, StringComparer.Ordinal));
            return Create(
                ArchitectureRemediationHintCategory.FixClassification,
                "Correct the declared role/classification before relocating code.",
                diagnostic,
                identity,
                Evidence("expected_roles", expectedRoles),
                expectedSeamOrDirection: expectedRoles);
        }

        return Review(diagnostic, identity);
    }

    internal static ArchitectureRemediationHint ForPublicApiSurface(
        PublicApiSurfaceDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.ReviewContract,
        "Review the declared public surface and selection evidence before changing code or snapshots.",
        diagnostic,
        identity,
        Evidence("public_surface", diagnostic.ApiAssemblyName ?? diagnostic.SourceType),
        caveat: "Do not rewrite a reviewed API snapshot or expand selection solely to make this finding disappear.",
        requiresReview: true);

    internal static ArchitectureRemediationHint ForAttributeUsage(
        AttributeUsageDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => ClassificationHint(
        diagnostic,
        identity,
        diagnostic.ExpectedAttributeLocation,
        diagnostic.ActualAttributeLocation,
        "attribute_location");

    internal static ArchitectureRemediationHint ForInheritance(
        InheritanceDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Review(diagnostic, identity);

    internal static ArchitectureRemediationHint ForInterfaceImplementation(
        InterfaceImplementationDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => ClassificationHint(
        diagnostic,
        identity,
        diagnostic.ExpectedImplementationLocation,
        diagnostic.ActualImplementationLocation,
        "implementation_location");

    internal static ArchitectureRemediationHint ForComposition(
        CompositionDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => diagnostic.ExpectedCompositionBoundary is null
        ? Review(diagnostic, identity)
        : Create(
            ArchitectureRemediationHintCategory.MoveCode,
            "Move composition work to the policy-declared composition boundary.",
            diagnostic,
            identity,
            Evidence("expected_composition_boundary", diagnostic.ExpectedCompositionBoundary),
            expectedSeamOrDirection: diagnostic.ExpectedCompositionBoundary);

    internal static ArchitectureRemediationHint ForProjectMetadata(
        ProjectMetadataDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.FixPolicyInput,
        "Correct the project metadata value required by the declared contract.",
        diagnostic,
        identity,
        Evidence("metadata_key", diagnostic.ProjectMetadataKey ?? diagnostic.ProjectMetadataKind ?? "project_metadata"),
        Evidence("expected_value", diagnostic.ProjectMetadataExpectedValue ?? string.Empty),
        caveat: "Keep the governed project metadata boundary intact while correcting the input.");

    internal static ArchitectureRemediationHint ForConfiguration(
        ConfigurationDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.FixPolicyInput,
        "Correct the identified policy or configuration input before changing application code.",
        diagnostic,
        identity,
        Evidence("configuration", diagnostic.TemplateName ?? diagnostic.ForbiddenNamespace));

    internal static ArchitectureRemediationHint ForContextDependency(
        ContextDependencyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Review(diagnostic, identity);

    internal static ArchitectureRemediationHint ForContextAllowOnly(
        ContextAllowOnlyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Review(diagnostic, identity);

    internal static ArchitectureRemediationHint ForPortBoundary(
        PortBoundaryDiagnostic diagnostic,
        ArchitectureViolationIdentity identity)
    {
        if (string.Equals(diagnostic.EvidenceKind, "unsupported_evidence", StringComparison.Ordinal))
        {
            return Create(
                ArchitectureRemediationHintCategory.FixPolicyInput,
                "Restore complete reference-resolution input before changing the reported dependency.",
                diagnostic,
                identity,
                Evidence("evidence_kind", diagnostic.EvidenceKind ?? "unsupported_evidence"));
        }

        if (string.IsNullOrWhiteSpace(diagnostic.ExpectedSeam))
        {
            return Review(diagnostic, identity);
        }

        ArchitectureRemediationHintCategory category = diagnostic.EvidenceKind is "adapter_context" or "adapter_port_mismatch"
            ? ArchitectureRemediationHintCategory.IntroduceAdapter
            : ArchitectureRemediationHintCategory.UseDeclaredPort;
        string summary = category == ArchitectureRemediationHintCategory.IntroduceAdapter
            ? "Repair the adapter against the declared adapter/port seam."
            : "Use the declared port seam instead of the direct cross-context dependency.";
        return Create(
            category,
            summary,
            diagnostic,
            identity,
            Evidence("evidence_kind", diagnostic.EvidenceKind ?? "port_boundary"),
            Evidence("expected_seam", diagnostic.ExpectedSeam),
            expectedSeamOrDirection: diagnostic.ExpectedSeam,
            caveat: "The declared seam is the only supported alternative; do not add a broad exception.");
    }

    internal static ArchitectureRemediationHint ForCycle(CycleDiagnostic diagnostic, ArchitectureViolationIdentity identity) =>
        Review(diagnostic, identity);

    internal static ArchitectureRemediationHint ForBuildStatePreflight(
        BuildStatePreflightDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.FixPolicyInput,
        "Restore the required project/build input before changing application structure.",
        diagnostic,
        identity,
        Evidence("preflight_state", diagnostic.State.ToString()),
        Evidence("project_path", diagnostic.Evidence.ProjectPath));

    internal static ArchitectureRemediationHint ForUnmatchedIgnore(
        UnmatchedIgnoreDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.NarrowException,
        "If an exception remains necessary, record only this exact edge through explicit review.",
        diagnostic,
        identity,
        Evidence("ignored_source", diagnostic.SourceType),
        Evidence("ignored_reference", diagnostic.ForbiddenReference),
        caveat: "Remove stale ignores; never replace them with wildcard or broad exclusions.",
        requiresReview: true);

    internal static ArchitectureRemediationHint ForPolicyConsistency(
        PolicyConsistencyDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.FixPolicyInput,
        "Resolve the policy consistency conflict before changing application code.",
        diagnostic,
        identity,
        Evidence("check_kind", diagnostic.CheckKind),
        caveat: "Keep policy coverage and enforcement intact while correcting the conflicting input.");

    internal static ArchitectureRemediationHint? ForBaseline(
        BaselineLifecycleDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => null;

    internal static ArchitectureRemediationHint ForPolicyError(
        ArchitecturePolicyErrorDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.FixPolicyInput,
        "Correct the reported policy input before evaluating application structure.",
        diagnostic,
        identity,
        Evidence("diagnostic_kind", diagnostic.DiagnosticKind.ToString()));

    internal static string CategoryToken(ArchitectureRemediationHintCategory category) => category switch
    {
        ArchitectureRemediationHintCategory.MoveCode => "move_code",
        ArchitectureRemediationHintCategory.DependOnAbstraction => "depend_on_abstraction",
        ArchitectureRemediationHintCategory.InvertDependency => "invert_dependency",
        ArchitectureRemediationHintCategory.IntroduceAdapter => "introduce_adapter",
        ArchitectureRemediationHintCategory.UseDeclaredPort => "use_declared_port",
        ArchitectureRemediationHintCategory.FixClassification => "fix_classification",
        ArchitectureRemediationHintCategory.FixPolicyInput => "fix_policy_input",
        ArchitectureRemediationHintCategory.NarrowException => "narrow_exception",
        ArchitectureRemediationHintCategory.RemoveOrReplaceDependency => "remove_or_replace_dependency",
        ArchitectureRemediationHintCategory.ReviewContract => "review_contract",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown remediation hint category."),
    };

    private static ArchitectureRemediationHint RemoveOrReplace(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        string evidenceKind,
        string evidenceValue) => Create(
        ArchitectureRemediationHintCategory.RemoveOrReplaceDependency,
        "Remove or replace the forbidden dependency; no approved alternative seam is evidenced.",
        diagnostic,
        identity,
        Evidence(evidenceKind, evidenceValue),
        caveat: NoApprovedSeamCaveat);

    private static ArchitectureRemediationHint ClassificationHint(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        string? expected,
        string? actual,
        string evidenceKind) => expected is null
        ? Review(diagnostic, identity)
        : Create(
            ArchitectureRemediationHintCategory.FixClassification,
            "Correct the declared classification/location before moving or changing dependencies.",
            diagnostic,
            identity,
            Evidence($"expected_{evidenceKind}", expected),
            Evidence($"actual_{evidenceKind}", actual ?? string.Empty),
            expectedSeamOrDirection: expected);

    private static ArchitectureRemediationHint Review(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity) => Create(
        ArchitectureRemediationHintCategory.ReviewContract,
        "Review the contract and existing policy evidence before choosing a structural repair.",
        diagnostic,
        identity,
        Evidence("diagnostic_kind", ArchitectureFindingMapper.KindToken(diagnostic.Kind)),
        caveat: NoApprovedSeamCaveat,
        requiresReview: true);

    private static ArchitectureRemediationHint Create(
        ArchitectureRemediationHintCategory category,
        string summary,
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        ArchitectureRemediationHintEvidence firstEvidence,
        ArchitectureRemediationHintEvidence? secondEvidence = null,
        string? expectedSeamOrDirection = null,
        string? caveat = null,
        bool requiresReview = false)
    {
        var evidence = new List<ArchitectureRemediationHintEvidence> { firstEvidence };
        if (secondEvidence is not null)
        {
            evidence.Add(secondEvidence);
        }

        return new ArchitectureRemediationHint(category, summary, identity.ContractId, identity, evidence)
        {
            ExpectedSeamOrDirection = expectedSeamOrDirection,
            Caveat = caveat,
            RequiresReview = requiresReview,
        };
    }

    private static ArchitectureRemediationHintEvidence Evidence(string kind, string value) => new(kind, value);

    private static bool IsCoverageDiagnostic(string forbiddenNamespace) =>
        forbiddenNamespace.StartsWith("uncovered ", StringComparison.Ordinal)
        || forbiddenNamespace.StartsWith("stale ", StringComparison.Ordinal)
        || forbiddenNamespace.StartsWith("unknown ", StringComparison.Ordinal);
}
