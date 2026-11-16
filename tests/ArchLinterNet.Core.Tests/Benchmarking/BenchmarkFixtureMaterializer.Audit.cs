using System.Text;

namespace ArchLinterNet.Core.Tests.Benchmarking;

internal static partial class BenchmarkFixtureMaterializer
{
    private static void AppendIgnoreEntries(
        StringBuilder content,
        BenchmarkWorkloadDefinition definition,
        int contract)
    {
        if (contract != 1 || definition.Dimensions.IgnoreRuleCount == 0) return;

        content.AppendLine("      ignored_violations:");
        for (int ignore = 1; ignore <= definition.Dimensions.IgnoreRuleCount; ignore++)
            content.AppendLine(
                    $"        - source_type: Synthetic.{definition.Projects[0].AssemblyName}.SourceRoot01Type{ignore:000}")
                .AppendLine("          forbidden_reference: Synthetic.Forbidden.Target")
                .AppendLine("          reason: Synthetic benchmark ignore dimension.");
    }

    private static void WriteContractLayerSources(string root, BenchmarkWorkloadDefinition definition)
    {
        BenchmarkContractLayerGraph? graph = definition.ContractLayerGraph;
        if (graph is null) return;

        BenchmarkProjectNode project = definition.Projects[0];
        string sourceDirectory = Path.Combine(root, "src", project.AssemblyName, "SourceRoot01");
        Directory.CreateDirectory(sourceDirectory);
        string[] sourceLines = graph.Layers.Select(layer =>
        {
            string namespaceName = ContractLayerNamespace(layer);
            StringBuilder lines = new StringBuilder()
                .AppendLine($"namespace {namespaceName}")
                .AppendLine("{")
                .AppendLine($"    public sealed class {ContractLayerType(layer)}")
                .AppendLine("    {");
            int edgeOrdinal = 0;
            foreach (BenchmarkContractLayerEdge edge in graph.Edges.Where(edge => edge.FromLayer == layer))
            {
                string targetType = $"global::{ContractLayerNamespace(edge.ToLayer)}.{ContractLayerType(edge.ToLayer)}";
                lines.AppendLine(
                        $"        public {targetType}? {ContractLayerType(edge.ToLayer)}Reference {{ get; set; }}")
                    .AppendLine(
                        $"        public static {targetType} CreateReference{edgeOrdinal++}() => new {targetType}();");
            }

            return lines.AppendLine("    }").AppendLine("}").ToString();
        }).ToArray();
        File.WriteAllText(Path.Combine(sourceDirectory, "ContractLayerGraph.cs"),
            string.Join(Environment.NewLine, sourceLines));
    }

    private static void AppendContractGraphLayers(StringBuilder content, BenchmarkWorkloadDefinition definition)
    {
        if (definition.Dimensions.IgnoreRuleCount > 0)
            content.AppendLine("  synthetic_forbidden_layer:")
                .AppendLine("    namespace: Synthetic.Forbidden")
                .AppendLine();

        if (definition.ContractLayerGraph is not { } graph) return;

        foreach (string layer in graph.Layers)
            content.AppendLine($"  {layer}:")
                .AppendLine($"    namespace: {ContractLayerNamespace(layer)}")
                .AppendLine();
    }

    private static void AppendMetricPolicy(StringBuilder content, BenchmarkWorkloadDefinition definition)
    {
        if (definition.Dimensions.MetricRuleCount == 0) return;

        content.AppendLine("topology:")
            .AppendLine("  subject_kind: type")
            .AppendLine("  mode: exhaustive")
            .AppendLine("  scope:")
            .AppendLine("    selectors:")
            .AppendLine("      - namespace: Synthetic")
            .AppendLine("  nodes:")
            .AppendLine("    - id: synthetic-metric-node")
            .AppendLine("      mappings:")
            .AppendLine("        - namespace: Synthetic");
        content.AppendLine("metrics:");
        for (int metric = 1; metric <= definition.Dimensions.MetricRuleCount; metric++)
            content.AppendLine($"  - id: synthetic-metric-{metric:000}")
                .AppendLine("    kind: topology_type_count")
                .AppendLine("    topology_node: synthetic-metric-node");
    }

    private static void AppendAuditContracts(StringBuilder content, BenchmarkWorkloadDefinition definition)
    {
        if (definition.ContractLayerGraph is not { } graph) return;

        content.AppendLine("  strict_cycles:")
            .AppendLine("    - id: synthetic-contract-layer-cycle")
            .AppendLine("      name: synthetic-contract-layer-cycle")
            .AppendLine($"      layers: [{string.Join(", ", graph.Layers)}]")
            .AppendLine("      reason: Exercise the executable synthetic contract-layer reference graph.");
        content.AppendLine("  strict_acyclic_siblings:")
            .AppendLine("    - id: synthetic-contract-layer-siblings")
            .AppendLine("      name: synthetic-contract-layer-siblings")
            .AppendLine("      ancestors: [Synthetic.ContractGraph]")
            .AppendLine("      reason: Exercise sibling graph construction over the same executable layer references.");
    }

    private static void AppendMethodBodyContracts(StringBuilder content, BenchmarkWorkloadDefinition definition)
    {
        if (definition.Dimensions.MethodBodyRuleCount == 0) return;

        content.AppendLine("  strict_method_body:");
        for (int rule = 1; rule <= definition.Dimensions.MethodBodyRuleCount; rule++)
            content.AppendLine($"    - id: synthetic-method-body-{rule:000}")
                .AppendLine($"      name: synthetic-method-body-{rule:000}")
                .AppendLine("      source: synthetic_layer_01")
                .AppendLine("      forbidden_calls: [System.Console.WriteLine]")
                .AppendLine("      reason: Exercise syntax-tree preparation and semantic symbol lookups.");
    }

    private static void WriteReportRuleCatalog(string directory, BenchmarkWorkloadDefinition definition)
    {
        if (definition.Dimensions.ReportRuleCount == 0) return;

        var reportRules = Enumerable.Range(1, definition.Dimensions.ReportRuleCount)
            .Select(index => new
            {
                id = $"synthetic-report-rule-{index:000}",
                name = $"Synthetic report rule {index:000}",
                short_description = "Synthetic benchmark SARIF rule registration input."
            })
            .ToArray();
        File.WriteAllText(Path.Combine(directory, "report-rules.json"), BenchmarkJson.Serialize(reportRules));
    }

    private static string ContractLayerNamespace(string layer)
    {
        return $"Synthetic.ContractGraph.{layer.Replace("contract_layer_", "Layer", StringComparison.Ordinal)}";
    }

    private static string ContractLayerType(string layer)
    {
        return layer.Replace("contract_layer_", "ContractLayer", StringComparison.Ordinal);
    }
}
