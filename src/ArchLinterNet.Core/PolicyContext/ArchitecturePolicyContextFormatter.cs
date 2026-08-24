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

        markdown.AppendLine();
        markdown.AppendLine("## Change-time guardrails");
        markdown.AppendLine($"- Policy weakening severity: `{Inline(context.Guardrails.PolicyWeakening)}`");

        AppendAnalysisInputs(markdown, context.Analysis);

        AppendLayers(markdown, context.Layers);
        AppendContracts(markdown, context.Contracts);
        AppendSourceExpansions(markdown, context.SourceExpansions);
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

    private static void AppendAnalysisInputs(StringBuilder markdown, ArchitecturePolicyContextAnalysis analysis)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Declared analysis scope");
        AppendAnalysisValues(markdown, "target assemblies", analysis.TargetAssemblies);
        AppendAnalysisValues(markdown, "projects", analysis.Projects);
        AppendAnalysisValues(markdown, "project include", analysis.ProjectInclude);
        AppendAnalysisValues(markdown, "project exclude", analysis.ProjectExclude);
        AppendAnalysisValues(markdown, "source roots", analysis.SourceRoots);
    }

    private static void AppendAnalysisValues(StringBuilder markdown, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            markdown.AppendLine($"- {label}: {string.Join(", ", values.Select(value => $"`{Inline(value)}`"))}");
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
            AppendContract(markdown, contract);
        }
    }

    private static void AppendContract(StringBuilder markdown, ArchitecturePolicyContextContract contract)
    {
        string reason = string.IsNullOrWhiteSpace(contract.Reason) ? string.Empty : $" — {Inline(contract.Reason)}";
        markdown.AppendLine(
            $"- **{Inline(contract.Mode)} / {Inline(contract.Family)}** `{Inline(contract.Id)}`: {Inline(contract.Name)}{reason}");
        foreach (ArchitecturePolicyContextReference reference in contract.References)
        {
            markdown.AppendLine($"  - {Inline(reference.Kind)}: {string.Join(", ", reference.Values.Select(value => $"`{Inline(value)}`"))}");
        }

        foreach (ArchitecturePolicyContextContractFact fact in contract.Facts.Where(fact => fact.Items.Count > 0))
        {
            AppendFact(markdown, fact, 2);
        }

        foreach (ArchitecturePolicyContextSelector selector in contract.Selectors)
        {
            markdown.AppendLine($"  - {FormatSelector(selector)}");
        }

        foreach (ArchitecturePolicyContextAdapterBinding binding in contract.AdapterBindings)
        {
            AppendAdapterBinding(markdown, binding);
        }

        foreach (string scope in contract.CoverageScopes)
        {
            markdown.AppendLine($"  - coverage scope: `{Inline(scope)}`");
        }
    }

    private static void AppendAdapterBinding(StringBuilder markdown, ArchitecturePolicyContextAdapterBinding binding)
    {
        markdown.AppendLine("  - adapter binding:");
        markdown.AppendLine($"    - {FormatSelector(binding.Adapter)}");
        markdown.AppendLine($"    - {FormatSelector(binding.ExpectedPort)}");
        foreach (ArchitecturePolicyContextSelector allowedContext in binding.AllowedContexts)
        {
            markdown.AppendLine($"    - {FormatSelector(allowedContext)}");
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

    private static void AppendSourceExpansions(
        StringBuilder markdown,
        IReadOnlyList<ArchitecturePolicyContextSourceExpansion> expansions)
    {
        if (expansions.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Source-set expansions");
        foreach (ArchitecturePolicyContextSourceExpansion expansion in expansions)
        {
            AppendSourceExpansion(markdown, expansion);
        }
    }

    private static void AppendSourceExpansion(StringBuilder markdown, ArchitecturePolicyContextSourceExpansion expansion)
    {
        List<string> details = new() { $"kind `{Inline(expansion.Kind)}`" };
        if (expansion.SetNames.Count > 0)
        {
            details.Add($"source sets {string.Join(", ", expansion.SetNames.Select(name => $"`{Inline(name)}`"))}");
        }

        if (!string.IsNullOrWhiteSpace(expansion.SelectorField)) details.Add($"field `{Inline(expansion.SelectorField)}`");
        if (expansion.OptionalEmpty) details.Add("optional-empty");
        if (!string.IsNullOrWhiteSpace(expansion.OptionalReason)) details.Add($"reason `{Inline(expansion.OptionalReason)}`");
        markdown.AppendLine($"- `{Inline(expansion.Group)}` `{Inline(expansion.AuthoredContractId)}`: {string.Join("; ", details)}");

        foreach (ArchitecturePolicyContextExpandedInstance instance in expansion.Instances)
        {
            markdown.AppendLine($"  - effective {FormatExpandedInstance(instance)}");
        }

        foreach (ArchitecturePolicyContextExpandedInstance inclusion in expansion.Inclusions)
        {
            markdown.AppendLine($"  - included {FormatExpandedInstance(inclusion)}");
        }

        foreach (ArchitecturePolicyContextExpandedExclusion exclusion in expansion.Exclusions)
        {
            AppendSourceExpansionExclusion(markdown, exclusion);
        }
    }

    private static void AppendSourceExpansionExclusion(StringBuilder markdown, ArchitecturePolicyContextExpandedExclusion exclusion)
    {
        string kind = exclusion.SetName is null ? "source" : "source set";
        string state = exclusion.Matched ? "matched" : "stale";
        string optional = exclusion.OptionalEmpty ? "; optional-empty" : string.Empty;
        string source = exclusion.SetName is not null && !string.IsNullOrWhiteSpace(exclusion.Source)
            ? $"; source `{Inline(exclusion.Source)}`"
            : string.Empty;
        string reason = string.IsNullOrWhiteSpace(exclusion.OptionalReason)
            ? string.Empty
            : $"; reason `{Inline(exclusion.OptionalReason)}`";
        string provenance = FormatProvenance(exclusion.Provenance);
        markdown.AppendLine($"  - excluded {kind} `{Inline(exclusion.SetName ?? exclusion.Source ?? string.Empty)}` ({state}{optional}){source}{reason}{provenance}");
    }

    private static void AppendFact(StringBuilder markdown, ArchitecturePolicyContextContractFact fact, int indent)
    {
        string prefix = new(' ', indent);
        string values = fact.Values.Count == 0
            ? string.Empty
            : $": {string.Join(", ", fact.Values.Select(value => $"`{Inline(value)}`"))}";
        markdown.AppendLine($"{prefix}- {Inline(fact.Name)}{values}");
        foreach (ArchitecturePolicyContextContractFact item in fact.Items)
        {
            AppendFact(markdown, item, indent + 2);
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

    private static string FormatExpandedInstance(ArchitecturePolicyContextExpandedInstance instance)
    {
        List<string> details = new() { $"contract `{Inline(instance.ContractId)}`" };
        if (!string.IsNullOrWhiteSpace(instance.Source)) details.Add($"source `{Inline(instance.Source)}`");
        if (!string.IsNullOrWhiteSpace(instance.SetName)) details.Add($"set `{Inline(instance.SetName)}`");
        if (!string.IsNullOrWhiteSpace(instance.Selector)) details.Add($"selector `{Inline(instance.Selector)}`");
        if (instance.OptionalEmpty) details.Add("optional-empty");
        if (!string.IsNullOrWhiteSpace(instance.OptionalReason)) details.Add($"reason `{Inline(instance.OptionalReason)}`");
        if (instance.SourceSetReferenceProvenance is not null)
        {
            details.Add($"source-set reference `{Inline(instance.SourceSetReferenceProvenance.YamlPath)}`");
        }

        return string.Join("; ", details);
    }

    private static string FormatProvenance(ArchitecturePolicyContextProvenance? provenance) => provenance is null
        ? string.Empty
        : $" at `{Inline(provenance.YamlPath)}`";

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
