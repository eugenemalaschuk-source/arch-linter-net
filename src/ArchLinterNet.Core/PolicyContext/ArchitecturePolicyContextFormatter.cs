using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.PolicyContext;

/// <summary>Formats policy-context exports for tools and agent prompts.</summary>
public static class ArchitecturePolicyContextFormatter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Formats one context export as deterministic, versioned JSON.</summary>
    public static string FormatAsJson(ArchitecturePolicyContextExport context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return JsonSerializer.Serialize(context, _jsonOptions);
    }

    /// <summary>Formats one context export as concise Markdown for agent prompts.</summary>
    public static string FormatAsMarkdown(ArchitecturePolicyContextExport context)
    {
        ArgumentNullException.ThrowIfNull(context);

        StringBuilder markdown = new();
        markdown.AppendLine("# Architecture policy context");
        markdown.AppendLine();
        markdown.AppendLine($"Policy: `{Inline(context.Policy.Name)}` (policy v{context.Policy.Version})  ");
        markdown.AppendLine($"Schema: `{Inline(context.Kind)}` v{context.SchemaVersion}");
        markdown.AppendLine();
        markdown.AppendLine("> This is an effective-policy summary. It does not build projects, analyze assemblies, or prove architecture compliance.");

        AppendLayers(markdown, context.Layers);
        AppendContracts(markdown, context.Contracts);
        AppendClassification(markdown, context.Classification, context.Contexts);
        AppendExceptions(markdown, context.Exceptions);

        markdown.AppendLine();
        markdown.AppendLine("## Safe agent guidance");
        foreach (string guidance in context.Guidance)
        {
            markdown.AppendLine($"- {guidance}");
        }

        return markdown.ToString().TrimEnd();
    }

    private static void AppendLayers(StringBuilder markdown, IReadOnlyList<ArchitecturePolicyContextLayer> layers)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Layers");
        if (layers.Count == 0)
        {
            markdown.AppendLine("- No layers declared.");
            return;
        }

        foreach (ArchitecturePolicyContextLayer layer in layers)
        {
            List<string> details = new();
            if (!string.IsNullOrWhiteSpace(layer.Namespace)) details.Add($"namespace `{Inline(layer.Namespace)}`");
            if (!string.IsNullOrWhiteSpace(layer.NamespaceSuffix)) details.Add($"suffix `{Inline(layer.NamespaceSuffix)}`");
            if (layer.External) details.Add("external");
            if (layer.Selector is not null) details.Add(FormatSelector(layer.Selector));
            markdown.AppendLine($"- `{Inline(layer.Name)}`: {string.Join("; ", details)}");
        }
    }

    private static void AppendContracts(StringBuilder markdown, IReadOnlyList<ArchitecturePolicyContextContract> contracts)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Active contracts");
        if (contracts.Count == 0)
        {
            markdown.AppendLine("- No strict or audit contracts declared.");
            return;
        }

        foreach (ArchitecturePolicyContextContract contract in contracts)
        {
            string reason = string.IsNullOrWhiteSpace(contract.Reason) ? string.Empty : $" — {Inline(contract.Reason)}";
            markdown.AppendLine(
                $"- **{Inline(contract.Mode)} / {Inline(contract.Family)}** `{Inline(contract.Id)}`: {Inline(contract.Name)}{reason}");
            foreach (ArchitecturePolicyContextReference reference in contract.References)
            {
                markdown.AppendLine($"  - {Inline(reference.Kind)}: {string.Join(", ", reference.Values.Select(value => $"`{Inline(value)}`"))}");
            }

            foreach (ArchitecturePolicyContextSelector selector in contract.Selectors)
            {
                markdown.AppendLine($"  - {FormatSelector(selector)}");
            }

            foreach (string scope in contract.CoverageScopes)
            {
                markdown.AppendLine($"  - coverage scope: `{Inline(scope)}`");
            }
        }
    }

    private static void AppendClassification(
        StringBuilder markdown,
        IReadOnlyList<ArchitecturePolicyContextClassification> classification,
        IReadOnlyList<ArchitecturePolicyContextValue> contexts)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Semantic roles and contexts");
        if (classification.Count == 0)
        {
            markdown.AppendLine("- No semantic classification mappings declared.");
        }
        else
        {
            foreach (ArchitecturePolicyContextClassification item in classification)
            {
                string metadata = FormatMetadata(item.Metadata);
                markdown.AppendLine($"- `{Inline(item.Role)}` via {Inline(item.Source)} `{Inline(item.Match)}`{metadata}");
            }
        }

        foreach (ArchitecturePolicyContextValue context in contexts)
        {
            markdown.AppendLine($"- context `{Inline(context.Key)}`: {string.Join(", ", context.Values.Select(value => $"`{Inline(value)}`"))}");
        }
    }

    private static void AppendExceptions(StringBuilder markdown, IReadOnlyList<ArchitecturePolicyContextException> exceptions)
    {
        if (exceptions.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Declared exceptions and exclusions");
        foreach (ArchitecturePolicyContextException exception in exceptions)
        {
            string reason = string.IsNullOrWhiteSpace(exception.Reason) ? string.Empty : $" — {Inline(exception.Reason)}";
            markdown.AppendLine(
                $"- `{Inline(exception.Scope)}` `{Inline(exception.Subject)}` {Inline(exception.Kind)}: {Inline(exception.Details)}{reason}");
        }
    }

    private static string FormatSelector(ArchitecturePolicyContextSelector selector)
    {
        string metadata = FormatMetadata(selector.Metadata);
        string when = string.IsNullOrWhiteSpace(selector.When) ? string.Empty : $" when `{Inline(selector.When)}`";
        return $"{Inline(selector.Kind)} selector: role `{Inline(selector.Role)}`{metadata}{when}";
    }

    private static string FormatMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count == 0)
        {
            return string.Empty;
        }

        return " (" + string.Join(", ", metadata.Select(item => $"{Inline(item.Key)}={Inline(item.Value)}")) + ")";
    }

    private static string Inline(string value) => value.Replace("`", "'").Replace("\r", " ").Replace("\n", " ");
}
