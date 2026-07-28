using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Schema;

/// <summary>Resolves the immutable schemas embedded in this Core package.</summary>
public sealed class PackagedSchemaRegistry
{
    private const string ManifestResourceName =
        "ArchLinterNet.Core.Schema.0.5.1.compatibility-manifest.json";
    private readonly Assembly _assembly;
    private readonly IReadOnlyDictionary<string, Entry> _entries;

    public PackagedSchemaRegistry() : this(typeof(PackagedSchemaRegistry).Assembly) { }

    internal PackagedSchemaRegistry(Assembly assembly)
    {
        _assembly = assembly;
        _entries = ReadManifest();
    }

    public IReadOnlyList<PackagedSchemaDescriptor> List() => _entries.Values
        .Select(static entry => entry.Descriptor)
        .OrderBy(static entry => entry.LogicalId, StringComparer.Ordinal)
        .ToArray();

    public bool TryRead(string logicalId, out string schema)
    {
        if (!_entries.TryGetValue(logicalId, out Entry? entry))
        {
            schema = string.Empty;
            return false;
        }

        schema = ReadAndValidate(entry);
        return true;
    }

    private IReadOnlyDictionary<string, Entry> ReadManifest()
    {
        using Stream stream = _assembly.GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException("The packaged schema manifest is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(static element => Entry.From(element))
            .ToDictionary(static entry => entry.Descriptor.LogicalId, StringComparer.Ordinal);
    }

    private string ReadAndValidate(Entry entry)
    {
        using Stream stream = _assembly.GetManifestResourceStream(entry.ResourceName)
            ?? throw new InvalidOperationException($"Packaged schema '{entry.Descriptor.LogicalId}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        if (!string.Equals(digest, entry.Descriptor.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Packaged schema '{entry.Descriptor.LogicalId}' has digest {digest}; expected {entry.Descriptor.Sha256}.");
        }

        using JsonDocument schema = JsonDocument.Parse(text);
        string id = schema.RootElement.GetProperty("$id").GetString() ?? string.Empty;
        if (!string.Equals(id, entry.Descriptor.SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Packaged schema '{entry.Descriptor.LogicalId}' has $id '{id}'; expected '{entry.Descriptor.SchemaId}'.");
        }

        return text;
    }

    private sealed record Entry(PackagedSchemaDescriptor Descriptor, string ResourceName)
    {
        public static Entry From(JsonElement element)
        {
            string resourcePath = element.GetProperty("resourcePath").GetString()!;
            PackagedSchemaDescriptor descriptor = new(
                element.GetProperty("logicalId").GetString()!,
                element.GetProperty("documentVersion").GetString()!, resourcePath,
                element.GetProperty("schemaId").GetString()!, element.GetProperty("sha256").GetString()!,
                element.GetProperty("supportsRead").GetBoolean(), element.GetProperty("supportsWrite").GetBoolean(),
                element.GetProperty("migrationNote").GetString()!, element.GetProperty("owningCapability").GetString()!);
            const string SchemaPrefix = "schema/";
            if (!resourcePath.StartsWith(SchemaPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Schema resource path '{resourcePath}' must start with '{SchemaPrefix}'.");
            }

            string resourceName = element.TryGetProperty("resourceName", out JsonElement resourceNameProperty)
                ? resourceNameProperty.GetString()!
                : "ArchLinterNet.Core.Schema." + resourcePath[SchemaPrefix.Length..].Replace('/', '.');
            return new Entry(descriptor, resourceName);
        }
    }
}
