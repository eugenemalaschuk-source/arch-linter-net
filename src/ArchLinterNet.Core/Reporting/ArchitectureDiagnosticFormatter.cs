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
                    string refs = string.Join(", ", violation.ForbiddenReferences);
                    string pathSuffix = string.Empty;
                    if (violation.DependencyPaths != null && violation.DependencyPaths.Count > 0)
                    {
                        var pathLines = violation.DependencyPaths
                            .Zip(violation.ForbiddenReferences, (path, reference) => (path, reference))
                            .Select(x => $"  via: {string.Join(" -> ", x.path)}");
                        pathSuffix = Environment.NewLine + string.Join(Environment.NewLine, pathLines);
                    }
                    return $"- {idPrefix}[{violation.ContractName}] {violation.SourceType} -> {violation.ForbiddenNamespace}{context}: {refs}{pathSuffix}";
                }));
    }

    public static string FormatCyclesForHumans(IReadOnlyCollection<string> cycles)
    {
        return string.Join(Environment.NewLine, cycles.OrderBy(c => c).Select(cycle => $"- {cycle}"));
    }

    public static string FormatResultForCiArtifacts(
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles)
    {
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

                if (v.TemplateName != null)
                    obj["template_name"] = v.TemplateName;

                if (v.ContainerNamespace != null)
                    obj["container_namespace"] = v.ContainerNamespace;

                if (v.DependencyPaths != null)
                    obj["dependency_paths"] = v.DependencyPaths.Select(p => p.ToArray()).ToArray();

                return obj;
            }).ToArray(),
            cycles = cycles.ToArray()
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

                if (v.TemplateName != null)
                    obj["template_name"] = v.TemplateName;

                if (v.ContainerNamespace != null)
                    obj["container_namespace"] = v.ContainerNamespace;

                if (v.DependencyPaths != null)
                    obj["dependency_paths"] = v.DependencyPaths.Select(p => p.ToArray()).ToArray();

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
