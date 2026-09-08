using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting.Abstractions;

namespace ArchLinterNet.Core.Reporting;

public sealed class ArchitectureDiagnosticFormatter : IArchitectureDiagnosticFormatter
{
    public static string FormatAssessmentCompletionForHumans(
        ArchitectureAssessmentCompletionEvidence? completion) =>
        ArchitectureApplicabilityHumanRenderer.RenderAssessmentCompletion(completion);

    public static string FormatApplicabilityProjectionForHumans(
        ArchitectureApplicabilityProjection? projection) =>
        ArchitectureApplicabilityHumanRenderer.RenderProjection(projection);

    public string FormatWaiversForHumans(IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers) =>
        ArchitectureWaiverLifecycleRenderer.RenderForHumans(waivers);

    public static string AddWaiversToCiArtifacts(
        string ciArtifacts,
        IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers) =>
        ArchitectureWaiverLifecycleRenderer.AddToCiArtifacts(ciArtifacts, waivers);

    public static string FormatPolicyInventoryForHumans(ArchitecturePolicyInventory? inventory) =>
        ArchitecturePolicyInventoryRenderer.RenderForHumans(inventory);

    public static string AddPolicyInventoryToCiArtifacts(
        string ciArtifacts,
        ArchitecturePolicyInventory? inventory) =>
        ArchitecturePolicyInventoryRenderer.AddToCiArtifacts(ciArtifacts, inventory);

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

    /// <summary>Formats already-normalized findings without recovering facts from display text.</summary>
    public static string FormatFindingsForHumans(
        IReadOnlyCollection<ArchitectureFinding> findings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(findings);
        IReadOnlyList<ArchitectureFinding> ordered = ArchitectureFindingMapper.Order(findings, cancellationToken);
        var lines = new string[ordered.Count];
        for (int index = 0; index < ordered.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines[index] = FormatFindingForHumans(ordered[index]);
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
                               $"    reason: {u.Reason}" + Reporting.ArchitectureDiagnosticFormatter.FormatPolicyLocationSuffix(u) +
                               (finding.RemediationHint is null
                                   ? string.Empty
                                   : FormatRemediationHintForHumans(finding.RemediationHint));
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
                               Reporting.ArchitectureDiagnosticFormatter.FormatPolicyLocationSuffix(f) +
                               (finding.RemediationHint is null
                                   ? string.Empty
                                   : FormatRemediationHintForHumans(finding.RemediationHint));
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
        ContractSurfaceExposureDiagnostic d => d.SourceType,
        AttributeUsageDiagnostic d => d.SourceType,
        InheritanceDiagnostic d => d.SourceType,
        InterfaceImplementationDiagnostic d => d.SourceType,
        CompositionDiagnostic d => d.SourceType,
        ProjectMetadataDiagnostic d => d.SourceType,
        MetricBudgetDiagnostic d => d.SourceType,
        ContextDependencyDiagnostic d => d.SourceType,
        ContextAllowOnlyDiagnostic d => d.SourceType,
        PortBoundaryDiagnostic d => d.SourceType,
        ImportedExternalDiagnostic d => d.SourceDiagnostic.RuleId ?? d.SelectedCanonicalIdentity,
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
        ContractSurfaceExposureDiagnostic d => d.ForbiddenNamespace,
        AttributeUsageDiagnostic d => d.ForbiddenNamespace,
        InheritanceDiagnostic d => d.ForbiddenNamespace,
        InterfaceImplementationDiagnostic d => d.ForbiddenNamespace,
        CompositionDiagnostic d => d.ForbiddenNamespace,
        ProjectMetadataDiagnostic d => d.ForbiddenNamespace,
        MetricBudgetDiagnostic d => d.ForbiddenNamespace,
        ContextDependencyDiagnostic d => d.ForbiddenNamespace,
        ContextAllowOnlyDiagnostic d => d.ForbiddenNamespace,
        PortBoundaryDiagnostic d => d.ForbiddenNamespace,
        ImportedExternalDiagnostic d => d.SourceDiagnostic.Project ?? string.Empty,
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
        ContractSurfaceExposureDiagnostic d => d.ForbiddenReferences,
        AttributeUsageDiagnostic d => d.ForbiddenReferences,
        InheritanceDiagnostic d => d.ForbiddenReferences,
        InterfaceImplementationDiagnostic d => d.ForbiddenReferences,
        CompositionDiagnostic d => d.ForbiddenReferences,
        ProjectMetadataDiagnostic d => d.ForbiddenReferences,
        MetricBudgetDiagnostic d => d.ForbiddenReferences,
        ContextDependencyDiagnostic d => d.ForbiddenReferences,
        ContextAllowOnlyDiagnostic d => d.ForbiddenReferences,
        PortBoundaryDiagnostic d => d.ForbiddenReferences,
        ImportedExternalDiagnostic => Array.Empty<string>(),
        _ => Array.Empty<string>()
    };

    private static string FormatForHumans(ArchitectureDiagnostic diagnostic)
    {
        if (diagnostic is ArchitectureApplicabilityDiagnostic applicability)
        {
            return ArchitectureApplicabilityHumanRenderer.RenderDiagnostic(applicability);
        }

        string idPrefix = diagnostic.ContractId != null ? $"[{diagnostic.ContractId}] " : string.Empty;
        string context = BuildHumanContext(diagnostic);

        string forbiddenNamespace = ForbiddenNamespaceOf(diagnostic);
        string nsDisplay = FormatNamespaceDisplayForHumans(forbiddenNamespace, diagnostic.MatchedNamespacePrefixes);

        string refs = string.Join(", ", ForbiddenReferencesOf(diagnostic));
        string pathSuffix = FormatConfigurationPathSuffixForHumans(diagnostic);

        return $"- {idPrefix}[{diagnostic.ContractName}] {SourceTypeOf(diagnostic)} -> {nsDisplay}{context}: " +
               $"{refs}{pathSuffix}{Reporting.ArchitectureDiagnosticFormatter.FormatPolicyLocationSuffix(diagnostic)}";
    }

    private static string FormatFindingForHumans(ArchitectureFinding finding)
    {
        string text = finding.Details is ImportedExternalDiagnostic imported
            ? ArchitectureImportedDiagnosticRenderer.RenderForHumans(imported, finding.CanonicalIdentity)
            : FormatForHumans(finding.Details);
        if (finding.RemediationHint is not null)
        {
            text += FormatRemediationHintForHumans(finding.RemediationHint);
        }

        return finding.Details is CompositionDiagnostic && finding.Identity is not null
            ? $"{text} (occurrence: {finding.Identity.Occurrence})"
            : text;
    }

    internal static string FormatRemediationHintForHumans(ArchitectureRemediationHint hint) =>
        $" (remediation: {ArchitectureRemediationHintFactory.CategoryToken(hint.Category)}: {hint.Summary})";

    internal static string FormatFindingForHumansInternal(ArchitectureFinding finding) =>
        FormatFindingForHumans(finding);

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
            context += Reporting.ArchitectureDiagnosticFormatter.FormatLayoutConventionContextForHumans(layoutConvention);
        }

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatPublicApiSurfaceContextForHumans(publicApiSurface);
        }

        if (diagnostic is ContractSurfaceExposureDiagnostic exposure)
        {
            context += ArchitectureContractSurfaceExposureRenderer.RenderForHumans(exposure);
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

        if (diagnostic is MetricBudgetDiagnostic metricBudget)
        {
            context += $" (kind: metric_budget, metric_id: {metricBudget.MetricId}, metric_kind: {metricBudget.MetricKind}, "
                + $"native_subject: {metricBudget.NativeSubject ?? "<none>"}, effective_scope: {metricBudget.EffectiveScope}, "
                + $"measured_value: {metricBudget.MeasuredValue}, breached_bound: {metricBudget.BreachedBound}, "
                + $"configured_limit: {metricBudget.ConfiguredLimit}, "
                + $"baseline_mode: {metricBudget.BaselineMode ?? "<none>"}, baseline_value: {metricBudget.BaselineValue?.ToString() ?? "<none>"}, "
                + $"delta: {metricBudget.Delta?.ToString() ?? "<none>"}, allowed_delta: {metricBudget.AllowedDelta?.ToString() ?? "<none>"}, "
                + $"effective_threshold: {metricBudget.EffectiveThreshold?.ToString() ?? "<none>"}, absolute_cap: {metricBudget.AbsoluteCap?.ToString() ?? "<none>"}, "
                + $"contributors: [{string.Join(", ", metricBudget.Contributors)}])";
        }

        if (diagnostic is ContextDependencyDiagnostic contextDependency)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatContextDependencyContextForHumans(contextDependency);
        }

        if (diagnostic is ContextAllowOnlyDiagnostic contextAllowOnly)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatContextAllowOnlyContextForHumans(contextAllowOnly);
        }

        if (diagnostic is PortBoundaryDiagnostic portBoundary)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatPortBoundaryContextForHumans(portBoundary);
        }

        if (diagnostic is FrameworkReferenceDiagnostic { Evidence.Count: > 0 } frameworkDependency)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatFrameworkReferenceContextForHumans(frameworkDependency.Evidence);
        }

        if (diagnostic is FrameworkReferenceAllowOnlyDiagnostic { Evidence.Count: > 0 } frameworkAllowOnly)
        {
            context += Reporting.ArchitectureDiagnosticFormatter.FormatFrameworkReferenceContextForHumans(frameworkAllowOnly.Evidence);
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

    internal static Dictionary<string, object?> ToCiJsonObject(
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
        obj["policy_origin"] = finding.PolicyOrigin is null ? null : Reporting.ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(finding.PolicyOrigin);
        obj["source_location"] = finding.SourceLocation is null
            ? null
            : new Dictionary<string, object?>
            {
                ["path"] = finding.SourceLocation.Path,
                ["line"] = finding.SourceLocation.Line,
                ["column"] = finding.SourceLocation.Column,
            };
        obj["baseline_state"] = finding.BaselineState;

        if (finding.RemediationHint is not null)
        {
            obj["remediation_guidance"] = FormatRemediationHintForJson(finding.RemediationHint);
        }

        if (includeContract)
        {
            obj["contract"] = diagnostic.ContractName;
            obj["contract_id"] = diagnostic.ContractId;
        }

        obj["source"] = SourceTypeOf(diagnostic);
        obj["forbidden_namespace"] = ForbiddenNamespaceOf(diagnostic);
        obj["forbidden_references"] = ForbiddenReferencesOf(diagnostic).ToArray();

        Reporting.ArchitectureDiagnosticFormatter.ApplyDiagnosticSpecificCiFields(diagnostic, obj);

        obj["details"] = BuildDetailsJsonObject(diagnostic);

        if (diagnostic.MatchedNamespacePrefixes != null)
        {
            obj["matched_namespace_prefixes"] = diagnostic.MatchedNamespacePrefixes.ToArray();
            if (diagnostic.MatchedNamespacePrefixes.Count == 1)
                obj["matched_namespace_prefix"] = diagnostic.MatchedNamespacePrefixes.First();
        }

        Reporting.ArchitectureDiagnosticFormatter.ApplyPolicyLocationFields(diagnostic, obj);

        return obj;
    }

    internal static void ApplyImportedExternalDiagnosticCiFields(
        ImportedExternalDiagnostic diagnostic,
        Dictionary<string, object?> obj) =>
        ArchitectureImportedDiagnosticRenderer.ApplyCiFields(diagnostic, obj);

    internal static Dictionary<string, object?> FormatNormalizedFindingForSarif(ArchitectureFinding finding) =>
        ToCiJsonObject(finding, includeContract: true);

    public static Dictionary<string, object?> FormatNormalizedFindingForJson(ArchitectureFinding finding) =>
        ToCiJsonObject(finding, includeContract: true);

    private static Dictionary<string, object?> FormatRemediationHintForJson(ArchitectureRemediationHint hint) => new()
    {
        ["category"] = ArchitectureRemediationHintFactory.CategoryToken(hint.Category),
        ["summary"] = hint.Summary,
        ["contract_identity"] = hint.ContractIdentity,
        ["finding_identity"] = ArchitectureViolationIdentityJson.ToWireObject(hint.FindingIdentity),
        ["evidence"] = hint.Evidence.Select(evidence => (object)new Dictionary<string, object?>
        {
            ["kind"] = evidence.Kind,
            ["value"] = evidence.Value,
        }).ToArray(),
        ["expected_seam_or_direction"] = hint.ExpectedSeamOrDirection,
        ["caveat"] = hint.Caveat,
        ["requires_review"] = hint.RequiresReview,
    };

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
        Reporting.ArchitectureDiagnosticFormatter.ApplyDiagnosticSpecificCiFields(diagnostic, details);
        return details;
    }

    internal static Dictionary<string, object?> ToUnmatchedJsonObject(
        UnmatchedIgnoreDiagnostic unmatched,
        string? mode)
    {
        return ToCiJsonObject(ArchitectureFindingMapper.FromDiagnostic(unmatched, mode), includeContract: true);
    }

    internal static string FormatPolicyLocationSuffix(ArchitectureDiagnostic diagnostic) => ArchitecturePolicyProvenanceProjector.FormatPolicyLocationSuffix(diagnostic);
    public static Dictionary<string, object?> FormatPolicyLocationForJson(ArchitecturePolicySourceLocation location) => ArchitecturePolicyProvenanceProjector.FormatPolicyLocationForJson(location);
    internal static void ApplyPolicyLocationFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> target) => ArchitecturePolicyProvenanceProjector.ApplyPolicyLocationFields(diagnostic, target);
    internal static string FormatLayoutConventionContextForHumans(LayoutConventionDiagnostic diagnostic) => ArchitectureLayoutConventionRenderer.FormatLayoutConventionContextForHumans(diagnostic);
    internal static string FormatPublicApiSurfaceContextForHumans(PublicApiSurfaceDiagnostic diagnostic) => ArchitecturePublicApiSurfaceRenderer.FormatPublicApiSurfaceContextForHumans(diagnostic);
    internal static string FormatContextDependencyContextForHumans(ContextDependencyDiagnostic diagnostic) => ArchitectureDiagnosticContextRenderer.FormatContextDependencyContextForHumans(diagnostic);
    internal static string FormatContextAllowOnlyContextForHumans(ContextAllowOnlyDiagnostic diagnostic) => ArchitectureDiagnosticContextRenderer.FormatContextAllowOnlyContextForHumans(diagnostic);
    internal static string FormatPortBoundaryContextForHumans(PortBoundaryDiagnostic diagnostic) => ArchitectureDiagnosticContextRenderer.FormatPortBoundaryContextForHumans(diagnostic);
    internal static string FormatFrameworkReferenceContextForHumans(IReadOnlyCollection<FrameworkReferenceEvidence> evidence) => ArchitectureFrameworkReferenceRenderer.FormatFrameworkReferenceContextForHumans(evidence);
    internal static string FormatWhenExpressionsForHumans(IReadOnlyList<ExpressionParticipation>? expressions) => ArchitectureDiagnosticContextRenderer.FormatWhenExpressionsForHumans(expressions);
    internal static void ApplyWhenExpressionsCiFields(IReadOnlyList<ExpressionParticipation>? expressions, Dictionary<string, object?> target) => ArchitectureDiagnosticContextRenderer.ApplyWhenExpressionsCiFields(expressions, target);
    internal static Dictionary<string, object?> ToPolicyConsistencyJsonObject(PolicyConsistencyDiagnostic finding, string? mode) => ArchitecturePolicyConsistencyProjector.ToPolicyConsistencyJsonObject(finding, mode);
    internal static Dictionary<string, object?> ToCycleJsonObject(ArchitectureCycleFinding finding, string? mode) => ArchitectureCycleRenderer.ToCycleJsonObject(finding, mode);
    internal static Dictionary<string, object?> ToCycleJsonObject(CycleDiagnostic finding, string? mode) => ArchitectureCycleRenderer.ToCycleJsonObject(finding, mode);
    internal static Dictionary<string, object?> ToCoverageSummaryJsonObject(ArchitectureCoverageSummary summary) => ArchitectureCoverageSummaryJsonProjector.ToCoverageSummaryJsonObject(summary);
    internal static object[] BuildStatePreflightJson(IReadOnlyCollection<BuildStatePreflightDiagnostic>? diagnostics, string mode) => ArchitectureBuildStatePreflightRenderer.BuildStatePreflightJson(diagnostics, mode);

    internal static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> target)
        => ArchitectureNormalizedDetailsProjector.ApplyDiagnosticSpecificCiFields(diagnostic, target);

    // Compatibility façade: the responsibility-specific renderers own the implementations below,
    // while this stable public type keeps the existing caller-facing overloads unchanged.
    public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => ArchitectureBuildStatePreflightRenderer.FormatBuildStatePreflightForHumans(diagnostics);
    public string FormatViolationsForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<ArchitectureViolation> violations) => ArchitectureViolationCiArtifactsRenderer.FormatViolationsForCiArtifacts(contractName, contractId, violations);
    public string FormatViolationsForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken) => ArchitectureViolationCiArtifactsRenderer.FormatViolationsForCiArtifacts(contractName, contractId, violations, cancellationToken);

    public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures); // NOSONAR: reviewed public compatibility overload

    public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, classificationRoles, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures); // NOSONAR: reviewed public compatibility overload

    public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, classificationRoles, classificationPathDeferred, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, classificationRoles, classificationPathDeferred, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures); // NOSONAR: reviewed public compatibility overload

    public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, classificationRoles, classificationPathDeferred, preflightDiagnostics, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null) => ArchitectureClassificationCiArtifactsRenderer.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, classificationRoles, classificationPathDeferred, preflightDiagnostics, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures); // NOSONAR: reviewed public compatibility overload

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics, ArchitectureSourceExpansionInventory sourceExpansion, IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null, IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null) => ArchitectureSourceExpansionProjector.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, classificationRoles, classificationPathDeferred, preflightDiagnostics, sourceExpansion, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures, subtractiveMatcherParticipation); // NOSONAR: reviewed public compatibility overload

    public static string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics, ArchitectureSourceExpansionInventory sourceExpansion, IReadOnlyCollection<ArchitectureViolation>? coverageFindings, IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched, IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures, IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation, CancellationToken cancellationToken) => ArchitectureSourceExpansionProjector.FormatResultForCiArtifacts(mode, passed, violations, cycles, cycleFindings, classificationRoles, classificationPathDeferred, preflightDiagnostics, sourceExpansion, coverageFindings, unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures, subtractiveMatcherParticipation, cancellationToken); // NOSONAR: reviewed public compatibility overload

    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures) =>
        ArchitectureClassificationCiArtifactsRenderer.FormatClassificationFactsForHumans(conflicts, metadataFailures);

    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) =>
        ArchitectureClassificationCiArtifactsRenderer.FormatClassificationFactsForHumans(conflicts, metadataFailures, classificationPathDeferred);

    public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles) =>
        ArchitectureCycleRenderer.FormatCyclesForHumans(cycles);

    public static string FormatCyclesForHumans(IReadOnlyCollection<ArchitectureCycleFinding> cycles) =>
        ArchitectureCycleRenderer.FormatCyclesForHumans(cycles);

    public string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles) =>
        ArchitectureCycleRenderer.FormatCyclesForCiArtifacts(contractName, contractId, cycles);

    public static string FormatCyclesForCiArtifacts(
        string contractName, string? contractId, IReadOnlyCollection<ArchitectureCycleFinding> cycles) =>
        ArchitectureCycleRenderer.FormatCyclesForCiArtifacts(contractName, contractId, cycles);
}
