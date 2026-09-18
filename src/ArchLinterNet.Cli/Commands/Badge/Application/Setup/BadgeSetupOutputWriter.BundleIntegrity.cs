using System.Security.Cryptography;
using System.Text.Json;
using static ArchLinterNet.Cli.Commands.Badge.Application.Setup.BadgeSetupValidationHelpers;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeRelayBundleIntegrityValidator
{
    internal static readonly string[] SourceNames =
    [
        "index.ts",
        "lifecycle.ts",
        "payload.ts",
        "read.ts",
        "registry-do.ts",
        "registry.ts",
        "relay-do.ts",
        "security.ts",
        "types.ts",
    ];

    internal static void Validate(string root, BadgeSetupConfiguration configuration) => Validate(root, configuration, requireTrustedManifest: true);

    internal static void ValidateGenerated(string root, BadgeSetupConfiguration configuration) => Validate(root, configuration, requireTrustedManifest: false);

    private static void Validate(string root, BadgeSetupConfiguration configuration, bool requireTrustedManifest)
    {
        string manifestPath = Path.Combine(root, "bundle-manifest.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw Integrity("bundle-manifest.json is missing or is not a regular file.");
        }

        try
        {
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            using JsonDocument document = JsonDocument.Parse(manifestBytes);
            JsonElement manifest = document.RootElement;
            ValidateManifestIdentity(manifest, configuration);
            ValidatePublisherPins(manifest);
            ValidateSourceFiles(root);
            List<(string Path, string Digest)> manifestEntries = ReadManifestEntries(manifest);
            ValidateTrustedManifest(manifestBytes, requireTrustedManifest);
            ValidateManifestFiles(root, manifestEntries);
        }
        catch (JsonException exception)
        {
            throw Integrity("bundle-manifest.json is malformed JSON.", exception);
        }
    }

    private static void ValidateManifestIdentity(JsonElement manifest, BadgeSetupConfiguration configuration)
    {
        RequireProperties(manifest, ["schema_id", "bundle", "compatibility_plan", "publisher_pins", "files"]);
        if (ManifestString(manifest, "schema_id") != "badge-relay-bundle-manifest/v1"
            || ManifestString(manifest, "bundle") != BadgeSetupContract.Bundle
            || ManifestString(manifest, "compatibility_plan") != BadgeSetupContract.CompatibilityPlan
            || configuration.Bundle != BadgeSetupContract.Bundle
            || configuration.CompatibilityPlan != BadgeSetupContract.CompatibilityPlan)
        {
            throw Integrity("bundle or compatibility identity is incompatible.");
        }
    }

    private static void ValidatePublisherPins(JsonElement manifest)
    {
        JsonElement pins = manifest.GetProperty("publisher_pins");
        RequireProperties(pins, ["workflow_ref", "workflow_sha", "action_ref", "action_sha", "commit"]);
        string workflowRef = ManifestString(pins, "workflow_ref");
        string workflowSha = ManifestString(pins, "workflow_sha");
        string actionRef = ManifestString(pins, "action_ref");
        string actionSha = ManifestString(pins, "action_sha");
        string commit = ManifestString(pins, "commit");
        if (!IsReusableWorkflowReference(workflowRef) || !IsSha(workflowSha, 40)
            || !IsPinnedActionReference(actionRef) || !IsSha(actionSha, 40) || !IsSha(commit, 40)
            || workflowRef != BadgeSetupContract.DefaultPublisherWorkflowRef
            || workflowSha != BadgeSetupContract.DefaultPublisherWorkflowSha
            || actionRef != BadgeSetupContract.DefaultActionRef
            || actionSha != BadgeSetupContract.DefaultPublisherActionSha
            || commit != BadgeSetupContract.DefaultPublisherWorkflowSha)
        {
            throw Integrity("publisher pins do not match the approved immutable commit.");
        }
    }

    private static void ValidateSourceFiles(string root)
    {
        string sourceRoot = Path.Combine(root, "src");
        if (!Directory.Exists(sourceRoot)) throw Integrity("src directory is missing.");
        string[] actualSourceNames = Directory.EnumerateFileSystemEntries(sourceRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(static name => name is not null)
            .Select(static name => name!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actualSourceNames.SequenceEqual(SourceNames.OrderBy(static name => name, StringComparer.Ordinal), StringComparer.Ordinal)
            || SourceNames.Any(name => !File.Exists(Path.Combine(sourceRoot, name))))
        {
            throw Integrity("src directory contains unexpected or missing files.");
        }
    }

    private static List<(string Path, string Digest)> ReadManifestEntries(JsonElement manifest)
    {
        JsonElement entries = manifest.GetProperty("files");
        if (entries.ValueKind != JsonValueKind.Array) throw Integrity("files must be an array.");
        string[] expected = [.. SourceNames.Select(static name => "src/" + name), "package.json", "package-lock.json", "tsconfig.json", "wrangler.jsonc", "THIRD-PARTY-NOTICES.txt", "schema/0.8.0/badge-relay-config.schema.json"];
        HashSet<string> seen = [];
        List<(string Path, string Digest)> manifestEntries = [];
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            RequireProperties(entry, ["path", "sha256"]);
            string path = ManifestString(entry, "path");
            string digest = ManifestString(entry, "sha256");
            if (!IsSafeManifestPath(path) || !seen.Add(path) || !expected.Contains(path, StringComparer.Ordinal))
            {
                throw Integrity($"manifest contains an unsafe, duplicate, or unexpected path '{path}'.");
            }

            manifestEntries.Add((path, digest));
        }

        if (seen.Count != expected.Length) throw Integrity("manifest is missing one or more required files.");
        return manifestEntries;
    }

    private static void ValidateTrustedManifest(byte[] manifestBytes, bool requireTrustedManifest)
    {
        if (requireTrustedManifest
            && !string.Equals(
                Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant(),
                BadgeSetupContract.ShippedRelayBundleManifestSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw Integrity("bundle-manifest.json does not match the trusted shipped manifest.");
        }
    }

    private static void ValidateManifestFiles(string root, IReadOnlyList<(string Path, string Digest)> manifestEntries)
    {
        foreach ((string path, string digest) in manifestEntries)
        {
            string setupRoot = Directory.GetParent(root)?.FullName
                ?? throw Integrity("Relay bundle parent directory is missing.");
            string filePath = path.StartsWith("schema/", StringComparison.Ordinal)
                ? Path.Combine(setupRoot, path.Replace('/', Path.DirectorySeparatorChar))
                : Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath) || new FileInfo(filePath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw Integrity($"manifest file '{path}' is missing or is not a regular file.");
            }

            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath))).ToLowerInvariant();
            if (!IsSha(digest, 64) || actual != digest) throw Integrity($"digest mismatch for '{path}'.");
        }
    }

    private static void RequireProperties(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Count() != expected.Length
            || value.EnumerateObject().Any(property => !expected.Contains(property.Name, StringComparer.Ordinal))
            || expected.Any(name => !value.TryGetProperty(name, out _)))
        {
            throw Integrity("bundle-manifest.json contains unknown or missing properties.");
        }
    }

    private static string ManifestString(JsonElement parent, string name) => parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? throw Integrity($"manifest property '{name}' is null.")
        : throw Integrity($"manifest property '{name}' is missing or not a string.");

    private static bool IsSafeManifestPath(string path) => !string.IsNullOrWhiteSpace(path)
        && !Path.IsPathRooted(path)
        && !path.Contains('\\')
        && !path.Contains("..", StringComparison.Ordinal)
        && !path.Contains("//", StringComparison.Ordinal);

    private static IOException Integrity(string detail, Exception? inner = null) => new("Local Relay bundle integrity validation failed: " + detail, inner);
}
