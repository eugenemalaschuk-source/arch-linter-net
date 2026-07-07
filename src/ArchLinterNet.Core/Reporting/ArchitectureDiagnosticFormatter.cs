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

    string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null);

    string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations);

    string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles);
}

public sealed class ArchitectureDiagnosticFormatter : IArchitectureDiagnosticFormatter
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

    public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        var diagnostics = cycles.Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null));
        return string.Join(Environment.NewLine, diagnostics.OrderBy(d => d.Path).Select(d => $"- {d.Path}"));
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
                               $"    reason: {u.Reason}";
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
                               (names.Length > 0 ? $" (contracts: {names})" : string.Empty);
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
            .Select(item => $"    excluded: {item.Item} ({item.Reason})");

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

    public string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null)
    {
        var unmatchedSerialized = (unmatched ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>())
            .Select(ArchitectureDiagnosticMapper.FromUnmatchedIgnore)
            .Select(u => new
            {
                contract = u.ContractName,
                contract_id = u.ContractId,
                ignore_index = u.IgnoreIndex,
                source_type = u.SourceType,
                forbidden_reference = u.ForbiddenReference,
                reason = u.Reason
            })
            .ToArray();

        var policyConsistencySerialized = (policyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>())
            .Select(ToPolicyConsistencyJsonObject)
            .ToArray();

        var payload = new
        {
            passed,
            mode,
            violations = violations
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            cycles = cycles
                .Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName: string.Empty, contractId: null))
                .Select(d => d.Path)
                .ToArray(),
            coverage_findings = (coverageFindings ?? Array.Empty<ArchitectureViolation>())
                .Select(ArchitectureDiagnosticMapper.FromViolation)
                .Select(d => ToCiJsonObject(d, includeContract: true))
                .ToArray(),
            unmatched_ignored_violations = unmatchedSerialized,
            policy_consistency_findings = policyConsistencySerialized,
            coverage_summary = (coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>())
                .OrderBy(s => s.ContractId ?? s.ContractName, StringComparer.Ordinal)
                .Select(ToCoverageSummaryJsonObject)
                .ToArray()
        };

        return JsonSerializer.Serialize(payload);
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

    public string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles)
    {
        var diagnostics = cycles.Select(cycle => ArchitectureDiagnosticMapper.FromCycle(cycle, contractName, contractId));

        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            contract_id = contractId,
            cycles = diagnostics.Select(d => d.Path).ToArray()
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
        _ => Array.Empty<string>()
    };

    private static string FormatForHumans(ArchitectureDiagnostic diagnostic)
    {
        string idPrefix = diagnostic.ContractId != null ? $"[{diagnostic.ContractId}] " : string.Empty;
        string context = string.Empty;

        if (diagnostic is DependencyDiagnostic { AllowedImporters: not null } dependency)
        {
            string srcLayer = dependency.SourceLayer ?? "?";
            string tgtLayer = dependency.TargetLayer ?? "?";
            string importers = string.Join(", ", dependency.AllowedImporters);
            context = $" (source_layer: {srcLayer}, target_layer: {tgtLayer}, allowed_importers: [{importers}])";
        }

        if (diagnostic is ExternalDependencyDiagnostic external)
        {
            context += $" (external_group: {external.ForbiddenExternalGroup})";
        }

        if (diagnostic is TypePlacementDiagnostic typePlacement)
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

            context += $" ({string.Join("; ", parts)})";
        }

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
        {
            string reason = publicApiSurface.ForbiddenPublicConstant == true
                ? "forbidden_public_constant"
                : "undeclared_api_member";
            context += $" (reason: {reason}, assembly: {publicApiSurface.ApiAssemblyName}, " +
                       $"visibility: {publicApiSurface.ApiVisibility}, signature: {publicApiSurface.UndeclaredApiSignature})";
        }

        if (diagnostic is AttributeUsageDiagnostic attributeUsage)
        {
            context += $" (kind: {attributeUsage.AttributeUsageKind}, attribute: {attributeUsage.MatchedAttribute}" +
                       (attributeUsage.ExpectedAttributeLocation != null
                           ? $", expected_location: {attributeUsage.ExpectedAttributeLocation}"
                           : string.Empty) +
                       (attributeUsage.ActualAttributeLocation != null
                           ? $", actual_location: {attributeUsage.ActualAttributeLocation}"
                           : string.Empty) +
                       ")";
        }

        string forbiddenNamespace = ForbiddenNamespaceOf(diagnostic);
        string nsDisplay = diagnostic.MatchedNamespacePrefixes switch
        {
            { Count: 1 } prefixes => $"{forbiddenNamespace} (matched {prefixes.First()})",
            { Count: > 1 } prefixes =>
                $"{forbiddenNamespace} (matched {string.Join(", ", prefixes.OrderBy(p => p, StringComparer.Ordinal))})",
            _ => forbiddenNamespace
        };

        string refs = string.Join(", ", ForbiddenReferencesOf(diagnostic));
        string pathSuffix = string.Empty;
        if (diagnostic is ConfigurationDiagnostic { DependencyPaths: { Count: > 0 } dependencyPaths } configuration)
        {
            var pathLines = dependencyPaths
                .Zip(configuration.ForbiddenReferences, (path, reference) => (path, reference))
                .Select(x => $"  via: {string.Join(" -> ", x.path)}");
            pathSuffix = Environment.NewLine + string.Join(Environment.NewLine, pathLines);
        }

        return $"- {idPrefix}[{diagnostic.ContractName}] {SourceTypeOf(diagnostic)} -> {nsDisplay}{context}: {refs}{pathSuffix}";
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

        return obj;
    }

    private static Dictionary<string, object?> ToCoverageSummaryJsonObject(ArchitectureCoverageSummary summary)
    {
        return new Dictionary<string, object?>
        {
            ["contract"] = summary.ContractName,
            ["contract_id"] = summary.ContractId,
            ["scope"] = summary.Scope,
            ["counts"] = new Dictionary<string, object?>
            {
                ["covered"] = summary.Counts.Covered,
                ["excluded"] = summary.Counts.Excluded,
                ["uncovered"] = summary.Counts.Uncovered,
                ["stale"] = summary.Counts.Stale,
                ["unknown"] = summary.Counts.Unknown
            },
            ["excluded_items"] = summary.ExcludedItems
                .OrderBy(item => item.Item, StringComparer.Ordinal)
                .Select(item => new Dictionary<string, object?> { ["item"] = item.Item, ["reason"] = item.Reason })
                .ToArray(),
            ["uncovered_items"] = ToEvidenceItemsJson(summary.UncoveredItems),
            ["stale_items"] = ToEvidenceItemsJson(summary.StaleItems),
            ["unknown_items"] = ToEvidenceItemsJson(summary.UnknownItems),
            ["covered_items"] = ToEvidenceItemsJson(summary.CoveredItems)
        };
    }

    private static Dictionary<string, object?>[] ToEvidenceItemsJson(
        IReadOnlyCollection<ArchitectureCoverageSummaryEvidenceItem> items)
    {
        return items
            .OrderBy(item => item.Item, StringComparer.Ordinal)
            .Select(item => new Dictionary<string, object?> { ["item"] = item.Item, ["evidence"] = item.Evidence })
            .ToArray();
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

        if (diagnostic is DependencyDiagnostic dependency)
        {
            if (dependency.SourceLayer != null)
                obj["source_layer"] = dependency.SourceLayer;

            if (dependency.TargetLayer != null)
                obj["target_layer"] = dependency.TargetLayer;

            if (dependency.AllowedImporters != null)
                obj["allowed_importers"] = dependency.AllowedImporters.ToArray();
        }

        if (diagnostic is ExternalDependencyDiagnostic external)
        {
            obj["forbidden_external_group"] = external.ForbiddenExternalGroup;
        }

        if (diagnostic is TypePlacementDiagnostic typePlacement)
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

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
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

        if (diagnostic is AttributeUsageDiagnostic attributeUsage)
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

        if (diagnostic is ConfigurationDiagnostic configuration)
        {
            if (configuration.TemplateName != null)
                obj["template_name"] = configuration.TemplateName;

            if (configuration.ContainerNamespace != null)
                obj["container_namespace"] = configuration.ContainerNamespace;

            if (configuration.DependencyPaths != null)
                obj["dependency_paths"] = configuration.DependencyPaths.Select(p => p.ToArray()).ToArray();
        }

        if (diagnostic.MatchedNamespacePrefixes != null)
        {
            obj["matched_namespace_prefixes"] = diagnostic.MatchedNamespacePrefixes.ToArray();
            if (diagnostic.MatchedNamespacePrefixes.Count == 1)
                obj["matched_namespace_prefix"] = diagnostic.MatchedNamespacePrefixes.First();
        }

        return obj;
    }
}
