using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public interface IArchitectureGraphFormatter
{
    string FormatAsJson(ArchitectureDependencyGraph graph);

    string FormatAsDot(ArchitectureDependencyGraph graph);

    string FormatAsMermaid(ArchitectureDependencyGraph graph);
}

public sealed class ArchitectureGraphFormatter : IArchitectureGraphFormatter
{
    public string FormatAsJson(ArchitectureDependencyGraph graph)
    {
        var payload = new
        {
            nodes = graph.Nodes.Select(node => new
            {
                id = node.Id,
                kind = node.Kind.ToString().ToLowerInvariant(),
            }),
            edges = graph.Edges.Select(edge => new
            {
                source = edge.SourceId,
                target = edge.TargetId,
                sourceKind = edge.SourceKind.ToString().ToLowerInvariant(),
                targetKind = edge.TargetKind.ToString().ToLowerInvariant(),
                contractIds = edge.ContractIds,
            }),
        };

        return JsonSerializer.Serialize(payload);
    }

    public string FormatAsDot(ArchitectureDependencyGraph graph)
    {
        StringBuilder builder = new();
        builder.AppendLine("digraph {");

        foreach (ArchitectureGraphEdge edge in graph.Edges)
        {
            string source = Quote(edge.SourceId);
            string target = Quote(edge.TargetId);

            if (edge.ContractIds.Count > 0)
            {
                string label = Quote(string.Join(", ", edge.ContractIds));
                builder.AppendLine($"  {source} -> {target} [label={label}];");
            }
            else
            {
                builder.AppendLine($"  {source} -> {target};");
            }
        }

        builder.Append('}');
        return builder.ToString();
    }

    public string FormatAsMermaid(ArchitectureDependencyGraph graph)
    {
        StringBuilder builder = new();
        builder.AppendLine("graph TD");

        Dictionary<string, string> aliases = new(StringComparer.Ordinal);
        int nextAlias = 0;

        string AliasFor(string id)
        {
            if (!aliases.TryGetValue(id, out string? alias))
            {
                alias = $"n{nextAlias++}";
                aliases[id] = alias;
            }

            return alias;
        }

        foreach (ArchitectureGraphEdge edge in graph.Edges)
        {
            string sourceAlias = AliasFor(edge.SourceId);
            string targetAlias = AliasFor(edge.TargetId);

            string label = edge.ContractIds.Count > 0
                ? $"|{string.Join(", ", edge.ContractIds)}|"
                : string.Empty;

            builder.AppendLine(
                $"  {sourceAlias}[\"{EscapeMermaidLabel(edge.SourceId)}\"] -->{label} {targetAlias}[\"{EscapeMermaidLabel(edge.TargetId)}\"]");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string EscapeMermaidLabel(string value)
    {
        return value.Replace("\"", "'");
    }
}
