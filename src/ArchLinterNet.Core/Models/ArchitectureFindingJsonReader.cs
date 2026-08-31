using System.Text.Json;

namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureFindingReadEnvelope(
    int SchemaVersion,
    string Kind,
    string CanonicalIdentity,
    string? Mode,
    string? Severity,
    string MessageCode,
    string? BaselineState,
    JsonElement RawDetails,
    bool IsOpaque)
{
    public string? Contract { get; init; }

    public string? ContractId { get; init; }

    public JsonElement? RawPolicyOrigin { get; init; }

    public JsonElement? RawSourceLocation { get; init; }

    /// <summary>Optional legacy remediation-hint value retained from the normalized envelope.</summary>
    public JsonElement? RawRemediationHint { get; init; }

    /// <summary>Optional structured remediation guidance retained from the normalized envelope.</summary>
    public JsonElement? RawRemediationGuidance { get; init; }

    /// <summary>The original normalized envelope, retained for lossless forwarding.</summary>
    public JsonElement RawEnvelope { get; init; }

    public string ToJson() => RawEnvelope.GetRawText();
}

/// <summary>
/// Reads the stable envelope without binding details to CLR subtype names. Supported kinds remain
/// available to higher-level typed consumers; unknown v1 kinds are retained as opaque payloads in
/// non-strict mode and rejected deterministically in strict mode.
/// </summary>
public static class ArchitectureFindingJsonReader
{
    private static readonly HashSet<string> _v1KnownKinds = new(StringComparer.Ordinal)
    {
        "dependency", "cycle", "unmatched_ignore", "configuration", "external_dependency",
        "policy_consistency", "package_dependency", "type_placement", "public_api_surface",
        "attribute_usage", "inheritance", "interface_implementation", "composition",
        "project_metadata", "context_dependency", "context_allow_only", "port_boundary",
        "layout_convention", "package_allow_only", "framework_reference",
        "framework_reference_allow_only", "build_state_preflight", "baseline",
        "architecture_policy_error",
    };

    private static readonly HashSet<string> _v2KnownKinds = new(_v1KnownKinds, StringComparer.Ordinal)
    {
        "applicability",
    };

    private static readonly HashSet<string> _v3KnownKinds = new(_v2KnownKinds, StringComparer.Ordinal)
    {
        "metric_budget",
        "imported_external_diagnostic",
    };

    public static ArchitectureFindingReadEnvelope Read(string json, bool strict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        int version = root.GetProperty("schema_version").GetInt32();
        HashSet<string>? knownKinds = version switch
        {
            1 => _v1KnownKinds,
            2 => _v2KnownKinds,
            ArchitectureFinding.CurrentSchemaVersion => _v3KnownKinds,
            _ => null,
        };
        if (knownKinds is null)
        {
            throw new ArchitectureFindingFormatException(
                $"Unsupported normalized finding schema version '{version}'.");
        }

        string kind = root.GetProperty("kind").GetString()
            ?? throw new ArchitectureFindingFormatException("Normalized finding kind is null.");
        bool opaque = !knownKinds.Contains(kind);
        if (strict && opaque)
        {
            throw new ArchitectureFindingFormatException(
                $"Unsupported normalized finding kind '{kind}' for schema version '{version}'.");
        }

        return new ArchitectureFindingReadEnvelope(
            version,
            kind,
            root.GetProperty("canonical_identity").GetString() ?? string.Empty,
            ReadNullableString(root, "mode"),
            ReadNullableString(root, "severity"),
            root.GetProperty("message_code").GetString() ?? string.Empty,
            ReadNullableString(root, "baseline_state"),
            root.GetProperty("details").Clone(),
            opaque)
        {
            Contract = ReadOptionalNullableString(root, "contract"),
            ContractId = ReadOptionalNullableString(root, "contract_id"),
            RawPolicyOrigin = ReadOptionalElement(root, "policy_origin"),
            RawSourceLocation = ReadOptionalElement(root, "source_location"),
            RawRemediationHint = ReadOptionalElement(root, "remediation_hint"),
            RawRemediationGuidance = ReadOptionalElement(root, "remediation_guidance"),
            RawEnvelope = root.Clone(),
        };
    }

    private static string? ReadNullableString(JsonElement root, string propertyName)
    {
        JsonElement value = root.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static string? ReadOptionalNullableString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static JsonElement? ReadOptionalElement(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement value) ? value.Clone() : null;
    }
}
