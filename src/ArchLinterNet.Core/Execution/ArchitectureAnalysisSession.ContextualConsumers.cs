using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    internal void RegisterContextualConsumer(ArchitectureContextSelector selector)
    {
        RegisterContextualConsumerCore(source: null, selector);
    }

    internal void RegisterContextualConsumer(
        ArchitectureContextSelector source,
        ArchitectureContextSelector selector)
    {
        RegisterContextualConsumerCore(source, selector);
    }

    private void RegisterContextualConsumerCore(
        ArchitectureContextSelector? source,
        ArchitectureContextSelector selector)
    {
        if (string.IsNullOrWhiteSpace(selector.Role))
        {
            return;
        }

        Dictionary<string, object> metadata = selector.Metadata
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Dictionary<string, object>? sourceMetadata = source == null
            ? null
            : source.Metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        string description = DescribeContextualConsumer(selector.Role, metadata, source?.Role, sourceMetadata);
        // `when` participates in identity (not just role/metadata): two selectors sharing one
        // role/metadata shape but declaring different `when` expressions are distinct consumption
        // records, not duplicates — collapsing them via TryAdd would silently drop one selector's
        // `when` from stale-selector coverage detection, keyed on declaration order.
        string identity = CreateContextualConsumerIdentity(
            selector.Role, metadata, selector.When, source?.Role, sourceMetadata, source?.When);
        _registeredContextualConsumers.TryAdd(identity,
            new ArchitectureContextualConsumerReference(
                selector.Role, metadata, description, source?.Role, sourceMetadata, selector.When, source?.When)
            {
                CompiledWhen = selector.CompiledWhen,
                SourceCompiledWhen = source?.CompiledWhen
            });
    }

    private static string DescribeContextualConsumer(
        string role,
        IReadOnlyDictionary<string, object> metadata,
        string? sourceRole,
        IReadOnlyDictionary<string, object>? sourceMetadata)
    {
        string selector = DescribeContextualSelector(role, metadata);
        return sourceRole == null
            ? selector
            : $"{selector} from {DescribeContextualSelector(sourceRole, sourceMetadata!)}";
    }

    private static string CreateContextualConsumerIdentity(
        string role,
        IReadOnlyDictionary<string, object> metadata,
        string? when,
        string? sourceRole,
        IReadOnlyDictionary<string, object>? sourceMetadata,
        string? sourceWhen)
    {
        string selector = $"{CreateContextualSelectorIdentity(role, metadata)};when={FormatContextualMetadataValue(when)}";
        return sourceRole == null
            ? selector
            : $"{CreateContextualSelectorIdentity(sourceRole, sourceMetadata!)};when={FormatContextualMetadataValue(sourceWhen)}=>{selector}";
    }

    private static string DescribeContextualSelector(string role, IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata.Count == 0)
        {
            return $"role:{role}";
        }

        return $"role:{role} metadata:{string.Join(",", metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}={FormatContextualMetadataDisplayValue(entry.Value)}"))}";
    }

    private static string CreateContextualSelectorIdentity(string role, IReadOnlyDictionary<string, object> metadata)
    {
        return $"role={FormatContextualMetadataValue(role)};metadata={string.Join(";", metadata
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{FormatContextualMetadataValue(entry.Key)}={FormatContextualMetadataValue(entry.Value)}"))}";
    }

    private static string FormatContextualMetadataValue(object? value)
    {
        if (value is System.Collections.IEnumerable sequence and not string)
        {
            return $"[{string.Join(",", sequence.Cast<object?>().Select(FormatContextualMetadataValue))}]";
        }

        return value switch
        {
            null => "null",
            string text => $"string:{text.Length}:{text}",
            IFormattable formattable => $"{value.GetType().FullName}:{formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)}",
            _ => $"{value.GetType().FullName}:{value}"
        };
    }

    private static string FormatContextualMetadataDisplayValue(object? value)
    {
        if (value is System.Collections.IEnumerable sequence and not string)
        {
            return $"[{string.Join(",", sequence.Cast<object?>().Select(FormatContextualMetadataDisplayValue))}]";
        }

        return value switch
        {
            null => "null",
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
