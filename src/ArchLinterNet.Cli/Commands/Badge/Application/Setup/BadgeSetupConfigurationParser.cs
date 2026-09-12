using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupConfigurationParser
{
    internal static BadgeSetupConfigurationParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid("Configuration input is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid("The configuration root must be an object.");
            }

            JsonElement root = document.RootElement;
            List<BadgeSetupDiagnostic> diagnostics = [];
            RejectUnknownProperties(root, [
                "schema_id", "contract_version", "mode", "disclosure_profile", "bundle",
                "compatibility_plan", "repository", "destination", "renewal", "pins", "managed_files",
            ], diagnostics);

            string? schemaId = ReadRequiredString(root, "schema_id", diagnostics);
            string? contractVersion = ReadRequiredString(root, "contract_version", diagnostics);
            string? mode = ReadRequiredString(root, "mode", diagnostics);
            string? profile = ReadRequiredString(root, "disclosure_profile", diagnostics);
            string? bundle = ReadRequiredString(root, "bundle", diagnostics);
            string? compatibilityPlan = ReadRequiredString(root, "compatibility_plan", diagnostics);
            BadgeSetupConfigurationRepository? repository = ReadRepository(root, diagnostics);
            BadgeSetupConfigurationDestination? destination = ReadDestination(root, diagnostics);
            BadgeSetupConfigurationRenewal? renewal = ReadRenewal(root, diagnostics);
            BadgeSetupPins? pins = ReadPins(root, diagnostics);
            IReadOnlyList<string>? managedFiles = ReadManagedFiles(root, diagnostics);

            if (diagnostics.Count != 0
                || schemaId is null
                || contractVersion is null
                || mode is null
                || profile is null
                || bundle is null
                || compatibilityPlan is null
                || repository is null
                || destination is null
                || renewal is null)
            {
                return new(false, null, diagnostics);
            }

            return new(
                true,
                new BadgeSetupConfiguration(
                    schemaId,
                    contractVersion,
                    mode,
                    profile,
                    bundle,
                    compatibilityPlan,
                    repository,
                    destination,
                    renewal,
                    pins,
                    managedFiles),
                []);
        }
        catch (JsonException exception)
        {
            return Invalid($"JSON parsing failed: {exception.GetType().Name}.");
        }
    }

    private static BadgeSetupConfigurationRepository? ReadRepository(
        JsonElement root,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!TryGetObject(root, "repository", out JsonElement value, diagnostics))
        {
            return null;
        }

        RejectUnknownProperties(value, ["owner", "name", "visibility"], diagnostics);
        string? owner = ReadRequiredString(value, "owner", diagnostics);
        string? name = ReadRequiredString(value, "name", diagnostics);
        string? visibility = ReadRequiredString(value, "visibility", diagnostics);
        return owner is null || name is null || visibility is null ? null : new(owner, name, visibility);
    }

    private static BadgeSetupConfigurationDestination? ReadDestination(
        JsonElement root,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!TryGetObject(root, "destination", out JsonElement value, diagnostics))
        {
            return null;
        }

        RejectUnknownProperties(value, ["alias", "account", "endpoint"], diagnostics);
        string? alias = ReadNullableString(value, "alias", diagnostics, required: true);
        string? account = ReadNullableString(value, "account", diagnostics);
        string? endpoint = ReadNullableString(value, "endpoint", diagnostics);
        return new(alias, account, endpoint);
    }

    private static BadgeSetupConfigurationRenewal? ReadRenewal(
        JsonElement root,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!TryGetObject(root, "renewal", out JsonElement value, diagnostics))
        {
            return null;
        }

        RejectUnknownProperties(value, ["enabled", "cadence_minutes", "max_lease_minutes"], diagnostics);
        bool? enabled = ReadBoolean(value, "enabled", diagnostics);
        int? cadence = ReadInteger(value, "cadence_minutes", diagnostics);
        int? lease = ReadInteger(value, "max_lease_minutes", diagnostics);
        return enabled is null || cadence is null || lease is null ? null : new(enabled.Value, cadence.Value, lease.Value);
    }

    private static BadgeSetupPins? ReadPins(JsonElement root, List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("pins", out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            AddInvalid(diagnostics, "pins must be an object.");
            return null;
        }

        RejectUnknownProperties(value, ["workflow_ref", "workflow_sha", "action_ref", "bundle_digest"], diagnostics);
        return new(
            ReadNullableString(value, "workflow_ref", diagnostics),
            ReadNullableString(value, "workflow_sha", diagnostics),
            ReadNullableString(value, "action_ref", diagnostics),
            ReadNullableString(value, "bundle_digest", diagnostics));
    }

    private static IReadOnlyList<string>? ReadManagedFiles(
        JsonElement root,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("managed_files", out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            AddInvalid(diagnostics, "managed_files must be an array.");
            return null;
        }

        List<string> files = [];
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                AddInvalid(diagnostics, "managed_files entries must be non-empty strings.");
                continue;
            }

            files.Add(item.GetString()!);
        }

        if (files.Count != files.Distinct(StringComparer.Ordinal).Count())
        {
            AddInvalid(diagnostics, "managed_files entries must be unique.");
        }

        return files;
    }

    private static bool TryGetObject(
        JsonElement root,
        string name,
        out JsonElement value,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        AddInvalid(diagnostics, $"{name} must be an object.");
        return false;
    }

    private static string? ReadRequiredString(
        JsonElement root,
        string name,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        string? value = ReadNullableString(root, name, diagnostics, required: true);
        return value;
    }

    private static string? ReadNullableString(
        JsonElement root,
        string name,
        List<BadgeSetupDiagnostic> diagnostics,
        bool required = false)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            if (required)
            {
                AddInvalid(diagnostics, $"Missing {name}.");
            }

            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            AddInvalid(diagnostics, $"{name} must be a non-empty string or null.");
            return null;
        }

        return value.GetString();
    }

    private static bool? ReadBoolean(JsonElement root, string name, List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out JsonElement value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            AddInvalid(diagnostics, $"{name} must be a boolean.");
            return null;
        }

        return value.GetBoolean();
    }

    private static int? ReadInteger(JsonElement root, string name, List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed))
        {
            AddInvalid(diagnostics, $"{name} must be an integer.");
            return null;
        }

        return parsed;
    }

    private static void RejectUnknownProperties(
        JsonElement objectElement,
        IEnumerable<string> allowed,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in objectElement.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name))
            {
                AddInvalid(diagnostics, "The configuration contains an unknown or duplicate property.");
            }
        }
    }

    private static BadgeSetupConfigurationParseResult Invalid(string detail) =>
        new(false, null, [BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidConfiguration, privateDetail: detail)]);

    private static void AddInvalid(List<BadgeSetupDiagnostic> diagnostics, string detail) =>
        diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidConfiguration, privateDetail: detail));
}
