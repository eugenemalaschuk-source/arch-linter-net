using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public partial interface IArchitectureDiagnosticFormatter
{
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    /// <summary>
    /// Additive overload, not a modification of the member above: any caller already compiled
    /// against the original one-parameter overload keeps resolving to it, unaffected. Declared
    /// with a default interface implementation that ignores the token and delegates to the
    /// original overload, so a third-party implementer that predates this member is not forced to
    /// add it just to keep compiling — only <see cref="ArchitectureDiagnosticFormatter"/> itself
    /// overrides it with a genuinely per-finding cancellation-aware implementation.
    /// </summary>
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via
        // ThrowIfCancellationRequested, not by forwarding it further.
        return FormatViolationsForHumans(violations); // NOSONAR: see comment above
    }

    string FormatCyclesForHumans(IReadOnlyCollection<string> cycles);

    string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched);

    string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> findings);

    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings);

    /// <summary>
    /// Cancellation-aware overload — coverage findings share the same shape (and can be equally
    /// large) as violations. Default interface implementation ignores the token and delegates to
    /// the overload above, so every existing test fake keeps compiling unaffected — only
    /// <see cref="ArchitectureDiagnosticFormatter"/> overrides it with a genuinely per-finding
    /// cancellation-aware implementation.
    /// </summary>
    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via
        // ThrowIfCancellationRequested, not by forwarding it further.
        return FormatCoverageForHumans(findings); // NOSONAR: see comment above
    }

    string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> summaries);

    string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures);

    /// <summary>
    /// Additive overload, not a modification of the member above: any caller already compiled
    /// against the original two-parameter overload keeps resolving to it, unaffected.
    /// <c>classificationPathDeferred</c> is required here, with no default value, so this overload
    /// stays unambiguous against the original for every call site, named or positional. Declared
    /// with a default interface implementation that delegates to the original overload and omits
    /// the path-deferred notice, so a third-party implementer that predates this member is not
    /// forced to add it just to keep compiling — only <see cref="ArchitectureDiagnosticFormatter"/>
    /// itself overrides it with real path-deferred formatting.
    /// </summary>
    string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
        => FormatClassificationFactsForHumans(conflicts, metadataFailures);

    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null);

    /// <summary>
    /// Additive overload, not a modification of the member above: any caller already compiled
    /// against the original ten-parameter overload keeps resolving to it, unaffected.
    /// <c>classificationRoles</c> is required here, with no default value, specifically so this
    /// overload stays unambiguous against the original for every call site, named or positional.
    /// Declared with a default interface implementation that delegates to the original overload
    /// and omits classification roles, so a third-party implementer that predates this member is
    /// not forced to add it just to keep compiling — only <see cref="ArchitectureDiagnosticFormatter"/>
    /// itself overrides it with real role serialization.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    /// <summary>
    /// Additive overload, not a modification of the member above: any caller already compiled
    /// against the eleven-parameter roles overload keeps resolving to it, unaffected.
    /// <c>classificationPathDeferred</c> is required here, with no default value, for the same
    /// unambiguous-arity reason as <c>classificationRoles</c> above. Declared with a default
    /// interface implementation that delegates to the roles overload and omits the path-deferred
    /// notice, so a third-party implementer that predates this member is not forced to add it —
    /// only <see cref="ArchitectureDiagnosticFormatter"/> itself overrides it with real serialization.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations);

    string FormatViolationsForCiArtifacts(
        string contractName,
        string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Existing external implementations can only provide the legacy member, which has no
        // cancellation parameter. The guard above still prevents entering that compatibility
        // path after cancellation; the concrete formatter overrides this overload and observes
        // the token throughout mapping, sorting, and serialization.
        return FormatViolationsForCiArtifacts(contractName, contractId, violations); // NOSONAR: preserve source compatibility for pre-cancellation interface implementers
    }

    string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles);
}

public sealed partial class ArchitectureDiagnosticFormatter : IArchitectureDiagnosticFormatter
{
    public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return FormatViolationsForHumans(violations, CancellationToken.None);
    }

    // Checked per finding — a large findings set is the dominant contributor to a large human
    // report, so this is the actual iteration boundary that needs to be interruptible, not just a
    // check before/after the whole call.
    public string FormatViolationsForHumans(
        IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.Order(
            ArchitectureFindingMapper.FromViolations(violations, mode: null, cancellationToken), cancellationToken);
        var lines = new string[findings.Count];
        for (int i = 0; i < findings.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines[i] = FormatFindingForHumans(findings[i]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched)
    {
        if (unmatched.Count == 0)
        {
            return string.Empty;
        }

        ArchitectureFinding[] findings = unmatched
            .Select(ArchitectureDiagnosticMapper.FromUnmatchedIgnore)
            .Select(ArchitectureFindingMapper.FromDiagnostic)
            .ToArray();

        return "Unmatched ignored violations:" + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                findings
                    .OrderBy(finding => finding.ContractName)
                    .ThenBy(finding => ((UnmatchedIgnoreDiagnostic)finding.Details).IgnoreIndex)
                    .Select(finding =>
                    {
                        var u = (UnmatchedIgnoreDiagnostic)finding.Details;
                        string idPrefix = u.ContractId != null ? $"[{u.ContractId}] " : string.Empty;
                        return $"  {idPrefix}[{u.ContractName}] ignored_violations[{u.IgnoreIndex}] no longer matches any current violation:{Environment.NewLine}" +
                               $"    source_type: {u.SourceType}{Environment.NewLine}" +
                               $"    forbidden_reference: {u.ForbiddenReference}{Environment.NewLine}" +
                               $"    reason: {u.Reason}" + FormatPolicyLocationSuffix(u);
                    }));
    }

    public string FormatPolicyConsistencyForHumans(
        IReadOnlyCollection<PolicyConsistencyDiagnostic> findings)
    {
        if (findings.Count == 0)
        {
            return string.Empty;
        }

        ArchitectureFinding[] normalized = findings
            .Select(finding => ArchitectureFindingMapper.FromDiagnostic(finding))
            .ToArray();
        return "Policy consistency findings:" + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                normalized
                    .OrderBy(finding => ((PolicyConsistencyDiagnostic)finding.Details).CheckKind, StringComparer.Ordinal)
                    .ThenBy(finding => finding.ContractName, StringComparer.Ordinal)
                    .Select(finding =>
                    {
                        var f = (PolicyConsistencyDiagnostic)finding.Details;
                        string idPrefix = f.ContractId != null ? $"[{f.ContractId}] " : string.Empty;
                        string names = string.Join(", ", f.ConflictingContractNames);
                        return $"  {idPrefix}[{f.CheckKind}] {f.Reason}" +
                               (names.Length > 0 ? $" (contracts: {names})" : string.Empty) +
                               FormatPolicyLocationSuffix(f);
                    }));
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings)
    {
        return FormatCoverageForHumans(findings, CancellationToken.None);
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings, CancellationToken cancellationToken)
    {
        if (findings.Count == 0)
        {
            return string.Empty;
        }

        return "Coverage findings:" + Environment.NewLine
            + FormatViolationsForHumans(findings, cancellationToken);
    }

    public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> summaries)
    {
        if (summaries.Count == 0)
        {
            return string.Empty;
        }

        var lines = summaries
            .OrderBy(s => s.ContractId ?? s.ContractName, StringComparer.Ordinal)
            .Select(FormatCoverageSummaryEntryForHumans);

        return "Coverage summary:" + Environment.NewLine
            + string.Join(Environment.NewLine, lines);
    }

    private static string FormatCoverageSummaryEntryForHumans(ArchitectureCoverageSummary summary)
    {
        string idPrefix = summary.ContractId != null ? $"[{summary.ContractId}] " : string.Empty;
        ArchitectureCoverageSummaryCounts counts = summary.Counts;

        string header = $"- {idPrefix}[{summary.ContractName}] scope: {summary.Scope} " +
            $"covered={counts.Covered} excluded={counts.Excluded} uncovered={counts.Uncovered} " +
            $"stale={counts.Stale} unknown={counts.Unknown} optional-empty={counts.OptionalEmpty}";

        var excludedLines = summary.ExcludedItems
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => string.IsNullOrEmpty(item.Evidence)
                ? $"    excluded: {item.Item} ({item.Reason})"
                : $"    excluded: {item.Item} ({item.Reason}; {item.Evidence})");

        var uncoveredLines = summary.UncoveredItems
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => $"    uncovered: {item.Item} ({item.Evidence})");

        var staleLines = summary.StaleItems
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => $"    stale: {item.Item} ({item.Evidence})");

        var unknownLines = summary.UnknownItems
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => $"    unknown: {item.Item} ({item.Evidence})");

        var optionalEmptyLines = summary.OptionalEmptyItems
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => $"    optional-empty: {item.Item} ({item.Reason}; {item.Evidence})" +
                (item.PolicyLocation is null
                    ? string.Empty
                    : $" (policy: {item.PolicyLocation.SourcePath}:{item.PolicyLocation.YamlPath})"));

        return string.Join(
            Environment.NewLine,
            new[] { header }.Concat(excludedLines).Concat(uncoveredLines).Concat(staleLines).Concat(unknownLines).Concat(optionalEmptyLines));
    }

    private static string SourceTypeOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
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
        CompositionDiagnostic d => d.SourceType,
        ProjectMetadataDiagnostic d => d.SourceType,
        ContextDependencyDiagnostic d => d.SourceType,
        ContextAllowOnlyDiagnostic d => d.SourceType,
        PortBoundaryDiagnostic d => d.SourceType,
        _ => string.Empty
    };

    private static string ForbiddenNamespaceOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic d => d.ForbiddenNamespace,
        ConfigurationDiagnostic d => d.ForbiddenNamespace,
        ExternalDependencyDiagnostic d => d.ForbiddenNamespace,
        PackageDependencyDiagnostic d => d.ForbiddenNamespace,
        PackageAllowOnlyDiagnostic d => d.ForbiddenNamespace,
        FrameworkReferenceDiagnostic d => d.ForbiddenNamespace,
        FrameworkReferenceAllowOnlyDiagnostic d => d.ForbiddenNamespace,
        TypePlacementDiagnostic d => d.ForbiddenNamespace,
        LayoutConventionDiagnostic d => d.ForbiddenNamespace,
        PublicApiSurfaceDiagnostic d => d.ForbiddenNamespace,
        AttributeUsageDiagnostic d => d.ForbiddenNamespace,
        InheritanceDiagnostic d => d.ForbiddenNamespace,
        InterfaceImplementationDiagnostic d => d.ForbiddenNamespace,
        CompositionDiagnostic d => d.ForbiddenNamespace,
        ProjectMetadataDiagnostic d => d.ForbiddenNamespace,
        ContextDependencyDiagnostic d => d.ForbiddenNamespace,
        ContextAllowOnlyDiagnostic d => d.ForbiddenNamespace,
        PortBoundaryDiagnostic d => d.ForbiddenNamespace,
        _ => string.Empty
    };

    private static IReadOnlyCollection<string> ForbiddenReferencesOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic d => d.ForbiddenReferences,
        ConfigurationDiagnostic d => d.ForbiddenReferences,
        ExternalDependencyDiagnostic d => d.ForbiddenReferences,
        PackageDependencyDiagnostic d => d.ForbiddenReferences,
        PackageAllowOnlyDiagnostic d => d.ForbiddenReferences,
        FrameworkReferenceDiagnostic d => d.ForbiddenReferences,
        FrameworkReferenceAllowOnlyDiagnostic d => d.ForbiddenReferences,
        TypePlacementDiagnostic d => d.ForbiddenReferences,
        LayoutConventionDiagnostic d => d.ForbiddenReferences,
        PublicApiSurfaceDiagnostic d => d.ForbiddenReferences,
        AttributeUsageDiagnostic d => d.ForbiddenReferences,
        InheritanceDiagnostic d => d.ForbiddenReferences,
        InterfaceImplementationDiagnostic d => d.ForbiddenReferences,
        CompositionDiagnostic d => d.ForbiddenReferences,
        ProjectMetadataDiagnostic d => d.ForbiddenReferences,
        ContextDependencyDiagnostic d => d.ForbiddenReferences,
        ContextAllowOnlyDiagnostic d => d.ForbiddenReferences,
        PortBoundaryDiagnostic d => d.ForbiddenReferences,
        _ => Array.Empty<string>()
    };

    private static string FormatForHumans(ArchitectureDiagnostic diagnostic)
    {
        string idPrefix = diagnostic.ContractId != null ? $"[{diagnostic.ContractId}] " : string.Empty;
        string context = BuildHumanContext(diagnostic);

        string forbiddenNamespace = ForbiddenNamespaceOf(diagnostic);
        string nsDisplay = FormatNamespaceDisplayForHumans(forbiddenNamespace, diagnostic.MatchedNamespacePrefixes);

        string refs = string.Join(", ", ForbiddenReferencesOf(diagnostic));
        string pathSuffix = FormatConfigurationPathSuffixForHumans(diagnostic);

        return $"- {idPrefix}[{diagnostic.ContractName}] {SourceTypeOf(diagnostic)} -> {nsDisplay}{context}: " +
               $"{refs}{pathSuffix}{FormatPolicyLocationSuffix(diagnostic)}";
    }

    private static string FormatFindingForHumans(ArchitectureFinding finding)
    {
        string text = FormatForHumans(finding.Details);
        return finding.Details is CompositionDiagnostic && finding.Identity is not null
            ? $"{text} (occurrence: {finding.Identity.Occurrence})"
            : text;
    }

    private static string BuildHumanContext(ArchitectureDiagnostic diagnostic)
    {
        string context = string.Empty;

        if (diagnostic is DependencyDiagnostic { AllowedImporters: not null } dependency)
        {
            context = FormatDependencyContextForHumans(dependency);
        }

        if (diagnostic is ExternalDependencyDiagnostic external)
        {
            context += $" (external_group: {external.ForbiddenExternalGroup})";
        }

        if (diagnostic is TypePlacementDiagnostic typePlacement)
        {
            context += FormatTypePlacementContextForHumans(typePlacement);
        }

        if (diagnostic is LayoutConventionDiagnostic layoutConvention)
        {
            context += FormatLayoutConventionContextForHumans(layoutConvention);
        }

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
        {
            context += FormatPublicApiSurfaceContextForHumans(publicApiSurface);
        }

        if (diagnostic is AttributeUsageDiagnostic attributeUsage)
        {
            context += FormatAttributeUsageContextForHumans(attributeUsage);
        }

        if (diagnostic is InheritanceDiagnostic inheritance)
        {
            context += FormatInheritanceContextForHumans(inheritance);
        }

        if (diagnostic is InterfaceImplementationDiagnostic interfaceImplementation)
        {
            context += FormatInterfaceImplementationContextForHumans(interfaceImplementation);
        }

        if (diagnostic is CompositionDiagnostic composition)
        {
            context += FormatCompositionContextForHumans(composition);
        }

        if (diagnostic is ProjectMetadataDiagnostic projectMetadata)
        {
            context += FormatProjectMetadataContextForHumans(projectMetadata);
        }

        if (diagnostic is ContextDependencyDiagnostic contextDependency)
        {
            context += FormatContextDependencyContextForHumans(contextDependency);
        }

        if (diagnostic is ContextAllowOnlyDiagnostic contextAllowOnly)
        {
            context += FormatContextAllowOnlyContextForHumans(contextAllowOnly);
        }

        if (diagnostic is PortBoundaryDiagnostic portBoundary)
        {
            context += FormatPortBoundaryContextForHumans(portBoundary);
        }

        if (diagnostic is FrameworkReferenceDiagnostic { Evidence.Count: > 0 } frameworkDependency)
        {
            context += FormatFrameworkReferenceContextForHumans(frameworkDependency.Evidence);
        }

        if (diagnostic is FrameworkReferenceAllowOnlyDiagnostic { Evidence.Count: > 0 } frameworkAllowOnly)
        {
            context += FormatFrameworkReferenceContextForHumans(frameworkAllowOnly.Evidence);
        }

        return context;
    }

    private static string FormatDependencyContextForHumans(DependencyDiagnostic dependency)
    {
        string srcLayer = dependency.SourceLayer ?? "?";
        string tgtLayer = dependency.TargetLayer ?? "?";
        string importers = string.Join(", ", dependency.AllowedImporters!);
        return $" (source_layer: {srcLayer}, target_layer: {tgtLayer}, allowed_importers: [{importers}])";
    }

    private static string FormatTypePlacementContextForHumans(TypePlacementDiagnostic typePlacement)
    {
        List<string> parts = new();
        if (typePlacement.ExpectedTypeLocation != null)
        {
            parts.Add($"expected_location: {typePlacement.ExpectedTypeLocation}, actual_location: {typePlacement.ActualTypeLocation}");
        }

        if (typePlacement.ExpectedTypeName != null)
        {
            parts.Add($"expected_name: {typePlacement.ExpectedTypeName}, actual_name: {typePlacement.ActualTypeName}");
        }

        return $" ({string.Join("; ", parts)})";
    }

    private static string FormatAttributeUsageContextForHumans(AttributeUsageDiagnostic attributeUsage)
    {
        return $" (kind: {attributeUsage.AttributeUsageKind}, attribute: {attributeUsage.MatchedAttribute}" +
               (attributeUsage.ExpectedAttributeLocation != null
                   ? $", expected_location: {attributeUsage.ExpectedAttributeLocation}"
                   : string.Empty) +
               (attributeUsage.ActualAttributeLocation != null
                   ? $", actual_location: {attributeUsage.ActualAttributeLocation}"
                   : string.Empty) +
               ")";
    }

    private static string FormatInheritanceContextForHumans(InheritanceDiagnostic inheritance)
    {
        return $" (forbidden_base_type: {inheritance.ForbiddenBaseType}" +
               (inheritance.InheritanceSourceSurface != null
                   ? $", source_surface: {inheritance.InheritanceSourceSurface}"
                   : string.Empty) +
               ")";
    }

    private static string FormatInterfaceImplementationContextForHumans(InterfaceImplementationDiagnostic interfaceImplementation)
    {
        return $" (kind: {interfaceImplementation.ImplementationKind}, interface: {interfaceImplementation.MatchedInterface}" +
               (interfaceImplementation.ExpectedImplementationLocation != null
                   ? $", expected_location: {interfaceImplementation.ExpectedImplementationLocation}"
                   : string.Empty) +
               (interfaceImplementation.ActualImplementationLocation != null
                   ? $", actual_location: {interfaceImplementation.ActualImplementationLocation}"
                   : string.Empty) +
               ")";
    }

    private static string FormatCompositionContextForHumans(CompositionDiagnostic composition)
    {
        return $" (matched_api: {composition.MatchedForbiddenApi}" +
               (composition.SourceAssembly != null
                   ? $", source_assembly: {composition.SourceAssembly}"
                   : string.Empty) +
               (composition.SourceMember != null
                   ? $", source_member: {composition.SourceMember}"
                   : string.Empty) +
               (composition.ExpectedCompositionBoundary != null
                   ? $", expected_boundary: {composition.ExpectedCompositionBoundary}"
                   : string.Empty) +
               ")";
    }

    private static string FormatProjectMetadataContextForHumans(ProjectMetadataDiagnostic projectMetadata)
    {
        return $" (kind: {projectMetadata.ProjectMetadataKind}" +
               (projectMetadata.ProjectMetadataKey != null
                   ? $", key: {projectMetadata.ProjectMetadataKey}"
                   : string.Empty) +
               (projectMetadata.ProjectMetadataExpectedValue != null
                   ? $", expected: {projectMetadata.ProjectMetadataExpectedValue}"
                   : string.Empty) +
               (projectMetadata.ProjectMetadataActualValue != null
                   ? $", actual: {projectMetadata.ProjectMetadataActualValue}"
                   : string.Empty) +
               (projectMetadata.ProjectMetadataSourcePath != null
                   ? $", source_path: {projectMetadata.ProjectMetadataSourcePath}"
                   : string.Empty) +
               ")";
    }

    private static string FormatNamespaceDisplayForHumans(string forbiddenNamespace, IReadOnlyCollection<string>? matchedNamespacePrefixes)
    {
        return matchedNamespacePrefixes switch
        {
            { Count: 1 } prefixes => $"{forbiddenNamespace} (matched {prefixes.First()})",
            { Count: > 1 } prefixes =>
                $"{forbiddenNamespace} (matched {string.Join(", ", prefixes.OrderBy(p => p, StringComparer.Ordinal))})",
            _ => forbiddenNamespace
        };
    }

    private static string FormatConfigurationPathSuffixForHumans(ArchitectureDiagnostic diagnostic)
    {
        if (diagnostic is ConfigurationDiagnostic { DependencyPaths: { Count: > 0 } dependencyPaths } configuration)
        {
            var pathLines = dependencyPaths
                .Zip(configuration.ForbiddenReferences, (path, reference) => (path, reference))
                .Select(x => $"  via: {string.Join(" -> ", x.path)}");
            return Environment.NewLine + string.Join(Environment.NewLine, pathLines);
        }

        return string.Empty;
    }

    private static Dictionary<string, object?> ToCiJsonObject(
        ArchitectureFinding finding,
        bool includeContract)
    {
        var obj = new Dictionary<string, object?>();
        ArchitectureDiagnostic diagnostic = finding.Details;

        // The versioned envelope is additive: callers that still consume the original
        // flat fields keep working, while new callers get an explicit discriminator
        // and family-owned evidence without inferring it from message text.
        obj["schema_version"] = finding.SchemaVersion;
        obj["kind"] = finding.Kind;
        obj["canonical_identity"] = finding.CanonicalIdentity;
        obj["mode"] = finding.Mode;
        obj["severity"] = finding.Severity;
        obj["message_code"] = finding.MessageCode;
        obj["policy_origin"] = finding.PolicyOrigin is null ? null : FormatPolicyLocationForJson(finding.PolicyOrigin);
        obj["source_location"] = finding.SourceLocation is null
            ? null
            : new Dictionary<string, object?>
            {
                ["path"] = finding.SourceLocation.Path,
                ["line"] = finding.SourceLocation.Line,
                ["column"] = finding.SourceLocation.Column,
            };
        obj["baseline_state"] = finding.BaselineState;

        if (includeContract)
        {
            obj["contract"] = diagnostic.ContractName;
            obj["contract_id"] = diagnostic.ContractId;
        }

        obj["source"] = SourceTypeOf(diagnostic);
        obj["forbidden_namespace"] = ForbiddenNamespaceOf(diagnostic);
        obj["forbidden_references"] = ForbiddenReferencesOf(diagnostic).ToArray();

        ApplyDiagnosticSpecificCiFields(diagnostic, obj);

        obj["details"] = BuildDetailsJsonObject(diagnostic);

        if (diagnostic.MatchedNamespacePrefixes != null)
        {
            obj["matched_namespace_prefixes"] = diagnostic.MatchedNamespacePrefixes.ToArray();
            if (diagnostic.MatchedNamespacePrefixes.Count == 1)
                obj["matched_namespace_prefix"] = diagnostic.MatchedNamespacePrefixes.First();
        }

        ApplyPolicyLocationFields(diagnostic, obj);

        return obj;
    }

    internal static Dictionary<string, object?> FormatNormalizedFindingForSarif(ArchitectureFinding finding) =>
        ToCiJsonObject(finding, includeContract: true);

    public static Dictionary<string, object?> FormatNormalizedFindingForJson(ArchitectureFinding finding) =>
        ToCiJsonObject(finding, includeContract: true);

    private static Dictionary<string, object?> BuildDetailsJsonObject(ArchitectureDiagnostic diagnostic)
    {
        var details = new Dictionary<string, object?>
        {
            ["detail_kind"] = ArchitectureFindingMapper.KindToken(diagnostic.Kind),
            ["contract"] = diagnostic.ContractName,
            ["contract_id"] = diagnostic.ContractId,
        };
        string source = SourceTypeOf(diagnostic);
        if (!string.IsNullOrEmpty(source))
        {
            details["source"] = source;
            details["forbidden_namespace"] = ForbiddenNamespaceOf(diagnostic);
            details["forbidden_references"] = ForbiddenReferencesOf(diagnostic).ToArray();
        }
        ApplyDiagnosticSpecificCiFields(diagnostic, details);
        return details;
    }

    private static Dictionary<string, object?> ToUnmatchedJsonObject(
        UnmatchedIgnoreDiagnostic unmatched,
        string? mode)
    {
        return ToCiJsonObject(ArchitectureFindingMapper.FromDiagnostic(unmatched, mode), includeContract: true);
    }

    private static void ApplyDependencyCiFields(DependencyDiagnostic dependency, Dictionary<string, object?> obj)
    {
        if (dependency.SourceLayer != null)
            obj["source_layer"] = dependency.SourceLayer;

        if (dependency.TargetLayer != null)
            obj["target_layer"] = dependency.TargetLayer;

        if (dependency.AllowedImporters != null)
            obj["allowed_importers"] = dependency.AllowedImporters.ToArray();
    }

    private static void ApplyTypePlacementCiFields(TypePlacementDiagnostic typePlacement, Dictionary<string, object?> obj)
    {
        if (typePlacement.ExpectedTypeLocation != null)
            obj["expected_type_location"] = typePlacement.ExpectedTypeLocation;

        if (typePlacement.ActualTypeLocation != null)
            obj["actual_type_location"] = typePlacement.ActualTypeLocation;

        if (typePlacement.ExpectedTypeName != null)
            obj["expected_type_name"] = typePlacement.ExpectedTypeName;

        if (typePlacement.ActualTypeName != null)
            obj["actual_type_name"] = typePlacement.ActualTypeName;
    }

    private static void ApplyAttributeUsageCiFields(AttributeUsageDiagnostic attributeUsage, Dictionary<string, object?> obj)
    {
        if (attributeUsage.MatchedAttribute != null)
            obj["matched_attribute"] = attributeUsage.MatchedAttribute;

        if (attributeUsage.AttributeUsageKind != null)
            obj["attribute_usage_kind"] = attributeUsage.AttributeUsageKind;

        if (attributeUsage.ExpectedAttributeLocation != null)
            obj["expected_attribute_location"] = attributeUsage.ExpectedAttributeLocation;

        if (attributeUsage.ActualAttributeLocation != null)
            obj["actual_attribute_location"] = attributeUsage.ActualAttributeLocation;
    }

    private static void ApplyInheritanceCiFields(InheritanceDiagnostic inheritance, Dictionary<string, object?> obj)
    {
        if (inheritance.ForbiddenBaseType != null)
            obj["forbidden_base_type"] = inheritance.ForbiddenBaseType;

        if (inheritance.InheritanceSourceSurface != null)
            obj["source_surface"] = inheritance.InheritanceSourceSurface;
    }

    private static void ApplyInterfaceImplementationCiFields(
        InterfaceImplementationDiagnostic interfaceImplementation, Dictionary<string, object?> obj)
    {
        if (interfaceImplementation.MatchedInterface != null)
            obj["matched_interface"] = interfaceImplementation.MatchedInterface;

        if (interfaceImplementation.ImplementationKind != null)
            obj["implementation_kind"] = interfaceImplementation.ImplementationKind;

        if (interfaceImplementation.ExpectedImplementationLocation != null)
            obj["expected_implementation_location"] = interfaceImplementation.ExpectedImplementationLocation;

        if (interfaceImplementation.ActualImplementationLocation != null)
            obj["actual_implementation_location"] = interfaceImplementation.ActualImplementationLocation;
    }

    private static void ApplyCompositionCiFields(CompositionDiagnostic composition, Dictionary<string, object?> obj)
    {
        if (composition.SourceMember != null)
            obj["source_member"] = composition.SourceMember;

        if (composition.MatchedForbiddenApi != null)
            obj["matched_forbidden_api"] = composition.MatchedForbiddenApi;

        if (composition.SourceAssembly != null)
            obj["source_assembly"] = composition.SourceAssembly;

        if (composition.ExpectedCompositionBoundary != null)
            obj["expected_composition_boundary"] = composition.ExpectedCompositionBoundary;
    }

    private static void ApplyProjectMetadataCiFields(ProjectMetadataDiagnostic projectMetadata, Dictionary<string, object?> obj)
    {
        if (projectMetadata.ProjectMetadataKind != null)
            obj["project_metadata_kind"] = projectMetadata.ProjectMetadataKind;

        if (projectMetadata.ProjectMetadataKey != null)
            obj["project_metadata_key"] = projectMetadata.ProjectMetadataKey;

        if (projectMetadata.ProjectMetadataExpectedValue != null)
            obj["project_metadata_expected_value"] = projectMetadata.ProjectMetadataExpectedValue;

        if (projectMetadata.ProjectMetadataActualValue != null)
            obj["project_metadata_actual_value"] = projectMetadata.ProjectMetadataActualValue;

        if (projectMetadata.ProjectMetadataSourcePath != null)
            obj["project_metadata_source_path"] = projectMetadata.ProjectMetadataSourcePath;
    }

    private static void ApplyConfigurationCiFields(ConfigurationDiagnostic configuration, Dictionary<string, object?> obj)
    {
        if (configuration.TemplateName != null)
            obj["template_name"] = configuration.TemplateName;

        if (configuration.ContainerNamespace != null)
            obj["container_namespace"] = configuration.ContainerNamespace;

        if (configuration.DependencyPaths != null)
            obj["dependency_paths"] = configuration.DependencyPaths.Select(p => p.ToArray()).ToArray();
    }
}
