using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public static class ArchitectureDiagnosticFormatter
{
    public static string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return string.Join(
            Environment.NewLine,
            violations
                .OrderBy(violation => violation.SourceType)
                .ThenBy(violation => violation.ForbiddenNamespace)
                .Select(violation =>
                {
                    string idPrefix = violation.ContractId != null ? $"[{violation.ContractId}] " : string.Empty;
                    string context = string.Empty;
                    if (violation.AllowedImporters != null)
                    {
                        string srcLayer = violation.SourceLayer ?? "?";
                        string tgtLayer = violation.TargetLayer ?? "?";
                        string importers = string.Join(", ", violation.AllowedImporters);
                        context = $" (source_layer: {srcLayer}, target_layer: {tgtLayer}, allowed_importers: [{importers}])";
                    }

                    if (violation.ForbiddenExternalGroup != null)
                    {
                        context += $" (external_group: {violation.ForbiddenExternalGroup})";
                    }

                    string nsDisplay = violation.MatchedNamespacePrefixes switch
                    {
                        { Count: 1 } prefixes => $"{violation.ForbiddenNamespace} (matched {prefixes.First()})",
                        { Count: > 1 } prefixes =>
                            $"{violation.ForbiddenNamespace} (matched {string.Join(", ", prefixes.OrderBy(p => p, StringComparer.Ordinal))})",
                        _ => violation.ForbiddenNamespace
                    };

                    string refs = string.Join(", ", violation.ForbiddenReferences);
                    string pathSuffix = string.Empty;
                    if (violation.DependencyPaths != null && violation.DependencyPaths.Count > 0)
                    {
                        var pathLines = violation.DependencyPaths
                            .Zip(violation.ForbiddenReferences, (path, reference) => (path, reference))
                            .Select(x => $"  via: {string.Join(" -> ", x.path)}");
                        pathSuffix = Environment.NewLine + string.Join(Environment.NewLine, pathLines);
                    }
                    return $"- {idPrefix}[{violation.ContractName}] {violation.SourceType} -> {nsDisplay}{context}: {refs}{pathSuffix}";
                }));
    }

    public static string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        return string.Join(Environment.NewLine, cycles.OrderBy(c => c).Select(cycle => $"- {cycle}"));
    }

    public static string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched)
    {
        if (unmatched.Count == 0)
        {
            return string.Empty;
        }

        return "Unmatched ignored violations:" + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                unmatched
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

    public static string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null)
    {
        var unmatchedSerialized = (unmatched ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>())
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

        var payload = new
        {
            passed,
            mode,
            violations = violations.Select(v =>
            {
                var obj = new Dictionary<string, object?>
                {
                    ["contract"] = v.ContractName,
                    ["contract_id"] = v.ContractId,
                    ["source"] = v.SourceType,
                    ["forbidden_namespace"] = v.ForbiddenNamespace,
                    ["forbidden_references"] = v.ForbiddenReferences.ToArray()
                };

                if (v.SourceLayer != null)
                    obj["source_layer"] = v.SourceLayer;

                if (v.TargetLayer != null)
                    obj["target_layer"] = v.TargetLayer;

                if (v.AllowedImporters != null)
                    obj["allowed_importers"] = v.AllowedImporters.ToArray();

                if (v.ForbiddenExternalGroup != null)
                    obj["forbidden_external_group"] = v.ForbiddenExternalGroup;

                if (v.TemplateName != null)
                    obj["template_name"] = v.TemplateName;

                if (v.ContainerNamespace != null)
                    obj["container_namespace"] = v.ContainerNamespace;

                if (v.DependencyPaths != null)
                    obj["dependency_paths"] = v.DependencyPaths.Select(p => p.ToArray()).ToArray();

                if (v.MatchedNamespacePrefixes != null)
                {
                    obj["matched_namespace_prefixes"] = v.MatchedNamespacePrefixes.ToArray();
                    if (v.MatchedNamespacePrefixes.Count == 1)
                        obj["matched_namespace_prefix"] = v.MatchedNamespacePrefixes.First();
                }

                return obj;
            }).ToArray(),
            cycles = cycles.ToArray(),
            unmatched_ignored_violations = unmatchedSerialized
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatViolationsForCiArtifacts(string contractName, string? contractId,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        var payload = new
        {
            kind = "architecture_violations",
            contract = contractName,
            contract_id = contractId,
            violations = violations.Select(v =>
            {
                var obj = new Dictionary<string, object?>
                {
                    ["source"] = v.SourceType,
                    ["forbidden_namespace"] = v.ForbiddenNamespace,
                    ["forbidden_references"] = v.ForbiddenReferences.ToArray()
                };

                if (v.SourceLayer != null)
                    obj["source_layer"] = v.SourceLayer;

                if (v.TargetLayer != null)
                    obj["target_layer"] = v.TargetLayer;

                if (v.AllowedImporters != null)
                    obj["allowed_importers"] = v.AllowedImporters.ToArray();

                if (v.ForbiddenExternalGroup != null)
                    obj["forbidden_external_group"] = v.ForbiddenExternalGroup;

                if (v.TemplateName != null)
                    obj["template_name"] = v.TemplateName;

                if (v.ContainerNamespace != null)
                    obj["container_namespace"] = v.ContainerNamespace;

                if (v.DependencyPaths != null)
                    obj["dependency_paths"] = v.DependencyPaths.Select(p => p.ToArray()).ToArray();

                if (v.MatchedNamespacePrefixes != null)
                {
                    obj["matched_namespace_prefixes"] = v.MatchedNamespacePrefixes.ToArray();
                    if (v.MatchedNamespacePrefixes.Count == 1)
                        obj["matched_namespace_prefix"] = v.MatchedNamespacePrefixes.First();
                }

                return obj;
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles)
    {
        var payload = new
        {
            kind = "architecture_cycles",
            contract = contractName,
            contract_id = contractId,
            cycles = cycles.ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

}
