using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public interface IArchitectureDiagnosticFormatter
{
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    string FormatCyclesForHumans(IReadOnlyCollection<string> cycles);

    string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched);

    string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> findings);

    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings);

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

    string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles);
}

public sealed partial class ArchitectureDiagnosticFormatter : IArchitectureDiagnosticFormatter
{
    public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        var diagnostics = violations.Select(ArchitectureDiagnosticMapper.FromViolation).ToArray();
        return string.Join(
            Environment.NewLine,
            diagnostics
                .OrderBy(d => SourceTypeOf(d))
                .ThenBy(d => ForbiddenNamespaceOf(d))
                .Select(FormatForHumans));
    }

    public string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched)
    {
        if (unmatched.Count == 0)
        {
            return string.Empty;
        }

        var diagnostics = unmatched.Select(ArchitectureDiagnosticMapper.FromUnmatchedIgnore).ToArray();

        return "Unmatched ignored violations:" + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                diagnostics
                    .OrderBy(u => u.ContractName)
                    .ThenBy(u => u.IgnoreIndex)
                    .Select(u =>
                    {
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

        return "Policy consistency findings:" + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                findings
                    .OrderBy(f => f.CheckKind, StringComparer.Ordinal)
                    .ThenBy(f => f.ContractName, StringComparer.Ordinal)
                    .Select(f =>
                    {
                        string idPrefix = f.ContractId != null ? $"[{f.ContractId}] " : string.Empty;
                        string names = string.Join(", ", f.ConflictingContractNames);
                        return $"  {idPrefix}[{f.CheckKind}] {f.Reason}" +
                               (names.Length > 0 ? $" (contracts: {names})" : string.Empty) +
                               FormatPolicyLocationSuffix(f);
                    }));
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings)
    {
        if (findings.Count == 0)
        {
            return string.Empty;
        }

        return "Coverage findings:" + Environment.NewLine
            + FormatViolationsForHumans(findings);
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
            $"stale={counts.Stale} unknown={counts.Unknown}";

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

        return string.Join(
            Environment.NewLine,
            new[] { header }.Concat(excludedLines).Concat(uncoveredLines).Concat(staleLines).Concat(unknownLines));
    }

    public string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        var payload = new
        {
            kind = "architecture_violations",
            contract = contractName,
            contract_id = contractId,
            violations = violations
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: false))
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string SourceTypeOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic d => d.SourceType,
        ConfigurationDiagnostic d => d.SourceType,
        ExternalDependencyDiagnostic d => d.SourceType,
        TypePlacementDiagnostic d => d.SourceType,
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
        TypePlacementDiagnostic d => d.ForbiddenNamespace,
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
        TypePlacementDiagnostic d => d.ForbiddenReferences,
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

    private static string FormatPublicApiSurfaceContextForHumans(PublicApiSurfaceDiagnostic publicApiSurface)
    {
        string reason = publicApiSurface.ForbiddenPublicConstant == true
            ? "forbidden_public_constant"
            : "undeclared_api_member";
        return $" (reason: {reason}, assembly: {publicApiSurface.ApiAssemblyName}, " +
               $"visibility: {publicApiSurface.ApiVisibility}, signature: {publicApiSurface.UndeclaredApiSignature})";
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

    private static Dictionary<string, object?> ToPolicyConsistencyJsonObject(PolicyConsistencyDiagnostic finding)
    {
        var obj = new Dictionary<string, object?>
        {
            ["kind"] = "policy_consistency",
            ["check_kind"] = finding.CheckKind,
            ["contract"] = finding.ContractName,
            ["contract_id"] = finding.ContractId,
            ["reason"] = finding.Reason,
            ["conflicting_contract_ids"] = finding.ConflictingContractIds.ToArray(),
            ["conflicting_contract_names"] = finding.ConflictingContractNames.ToArray(),
            ["layers"] = finding.Layers.ToArray()
        };

        if (finding.RepresentativeType != null)
        {
            obj["representative_type"] = finding.RepresentativeType;
        }

        ApplyPolicyLocationFields(finding, obj);

        return obj;
    }

    private static Dictionary<string, object?> ToCiJsonObject(ArchitectureDiagnostic diagnostic, bool includeContract)
    {
        var obj = new Dictionary<string, object?>();

        if (includeContract)
        {
            obj["contract"] = diagnostic.ContractName;
            obj["contract_id"] = diagnostic.ContractId;
        }

        obj["source"] = SourceTypeOf(diagnostic);
        obj["forbidden_namespace"] = ForbiddenNamespaceOf(diagnostic);
        obj["forbidden_references"] = ForbiddenReferencesOf(diagnostic).ToArray();

        ApplyDiagnosticSpecificCiFields(diagnostic, obj);

        if (diagnostic.MatchedNamespacePrefixes != null)
        {
            obj["matched_namespace_prefixes"] = diagnostic.MatchedNamespacePrefixes.ToArray();
            if (diagnostic.MatchedNamespacePrefixes.Count == 1)
                obj["matched_namespace_prefix"] = diagnostic.MatchedNamespacePrefixes.First();
        }

        ApplyPolicyLocationFields(diagnostic, obj);

        return obj;
    }

    private static Dictionary<string, object?> ToUnmatchedJsonObject(UnmatchedIgnoreDiagnostic unmatched)
    {
        var obj = new Dictionary<string, object?>
        {
            ["contract"] = unmatched.ContractName,
            ["contract_id"] = unmatched.ContractId,
            ["ignore_index"] = unmatched.IgnoreIndex,
            ["source_type"] = unmatched.SourceType,
            ["forbidden_reference"] = unmatched.ForbiddenReference,
            ["reason"] = unmatched.Reason
        };
        ApplyPolicyLocationFields(unmatched, obj);
        return obj;
    }

    private static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic is DependencyDiagnostic dependency)
        {
            ApplyDependencyCiFields(dependency, obj);
        }

        if (diagnostic is ExternalDependencyDiagnostic external)
        {
            obj["forbidden_external_group"] = external.ForbiddenExternalGroup;
        }

        if (diagnostic is TypePlacementDiagnostic typePlacement)
        {
            ApplyTypePlacementCiFields(typePlacement, obj);
        }

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
        {
            ApplyPublicApiSurfaceCiFields(publicApiSurface, obj);
        }

        if (diagnostic is AttributeUsageDiagnostic attributeUsage)
        {
            ApplyAttributeUsageCiFields(attributeUsage, obj);
        }

        if (diagnostic is InheritanceDiagnostic inheritance)
        {
            ApplyInheritanceCiFields(inheritance, obj);
        }

        if (diagnostic is InterfaceImplementationDiagnostic interfaceImplementation)
        {
            ApplyInterfaceImplementationCiFields(interfaceImplementation, obj);
        }

        if (diagnostic is CompositionDiagnostic composition)
        {
            ApplyCompositionCiFields(composition, obj);
        }

        if (diagnostic is ProjectMetadataDiagnostic projectMetadata)
        {
            ApplyProjectMetadataCiFields(projectMetadata, obj);
        }

        if (diagnostic is ConfigurationDiagnostic configuration)
        {
            ApplyConfigurationCiFields(configuration, obj);
        }

        if (diagnostic is ContextDependencyDiagnostic contextDependency)
        {
            ApplyContextDependencyCiFields(contextDependency, obj);
        }

        if (diagnostic is ContextAllowOnlyDiagnostic contextAllowOnly)
        {
            ApplyContextAllowOnlyCiFields(contextAllowOnly, obj);
        }

        if (diagnostic is PortBoundaryDiagnostic portBoundary)
        {
            ApplyPortBoundaryCiFields(portBoundary, obj);
        }
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

    private static void ApplyPublicApiSurfaceCiFields(PublicApiSurfaceDiagnostic publicApiSurface, Dictionary<string, object?> obj)
    {
        if (publicApiSurface.UndeclaredApiSignature != null)
            obj["undeclared_api_signature"] = publicApiSurface.UndeclaredApiSignature;

        if (publicApiSurface.ForbiddenPublicConstant != null)
            obj["forbidden_public_constant"] = publicApiSurface.ForbiddenPublicConstant;

        if (publicApiSurface.ApiAssemblyName != null)
            obj["assembly"] = publicApiSurface.ApiAssemblyName;

        if (publicApiSurface.ApiVisibility != null)
            obj["visibility"] = publicApiSurface.ApiVisibility;
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
