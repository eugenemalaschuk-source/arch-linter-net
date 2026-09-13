using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeRelayBundleIntegrityValidatorTests
{
    [Test]
    public void MissingManifestIsRejected()
    {
        AssertRejected(root => File.Delete(Path.Combine(root, "bundle-manifest.json")), "missing or is not a regular file");
    }

    [Test]
    public void MalformedManifestIsRejected()
    {
        AssertRejected(root => File.WriteAllText(Path.Combine(root, "bundle-manifest.json"), "{"), "malformed JSON");
    }

    [Test]
    public void MissingManifestPropertyIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest => manifest.Remove("files")), "unknown or missing properties");
    }

    [Test]
    public void IncompatibleBundleIdentityIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest => manifest["bundle"] = "badge-relay/v2"), "bundle or compatibility identity is incompatible");
    }

    [Test]
    public void InvalidPublisherPinsShapeIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest => manifest["publisher_pins"] = new JsonObject()), "unknown or missing properties");
    }

    [Test]
    public void UnapprovedPublisherPinValueIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest =>
        {
            JsonObject pins = (JsonObject)manifest["publisher_pins"]!;
            pins["workflow_sha"] = new string('a', 40);
        }), "publisher pins do not match the approved immutable commit");
    }

    [Test]
    public void MissingSourceDirectoryIsRejected()
    {
        AssertRejected(root => Directory.Delete(Path.Combine(root, "src"), recursive: true), "src directory is missing");
    }

    [Test]
    public void MissingSourceFileIsRejected()
    {
        AssertRejected(root => File.Delete(Path.Combine(root, "src", "types.ts")), "src directory contains unexpected or missing files");
    }

    [Test]
    public void UnsafeManifestPathIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest =>
        {
            JsonObject first = (JsonObject)((JsonArray)manifest["files"]!)[0]!;
            first["path"] = "../index.ts";
        }), "unsafe, duplicate, or unexpected path");
    }

    [Test]
    public void NonStringManifestPathIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest =>
        {
            JsonObject first = (JsonObject)((JsonArray)manifest["files"]!)[0]!;
            first["path"] = 42;
        }), "missing or not a string");
    }

    [Test]
    public void MissingManifestFileIsRejected()
    {
        AssertRejected(root => File.Delete(Path.Combine(root, "package.json")), "is missing or is not a regular file");
    }

    [Test]
    public void UnexpectedManifestEntryPropertyIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest =>
        {
            JsonObject first = (JsonObject)((JsonArray)manifest["files"]!)[0]!;
            first["unexpected"] = true;
        }), "unknown or missing properties");
    }

    [Test]
    public void MissingManifestEntryIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest =>
        {
            JsonArray files = (JsonArray)manifest["files"]!;
            files.RemoveAt(files.Count - 1);
        }), "missing one or more required files");
    }

    [Test]
    public void NonArrayManifestFilesIsRejected()
    {
        AssertRejected(root => EditManifest(root, manifest => manifest["files"] = new JsonObject()), "files must be an array");
    }

    private static void AssertRejected(Action<string> mutate, string expectedMessage)
    {
        string root = CopyRelayBundle(out string temporary);
        try
        {
            mutate(root);
            IOException? exception = Assert.Throws<IOException>(() => BadgeRelayBundleIntegrityValidator.Validate(root, Configuration()));
            Assert.That(exception!.Message, Does.Contain("Local Relay bundle integrity validation failed"));
            Assert.That(exception.Message, Does.Contain(expectedMessage));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void EditManifest(string root, Action<JsonObject> edit)
    {
        string path = Path.Combine(root, "bundle-manifest.json");
        JsonObject manifest = (JsonObject)(JsonNode.Parse(File.ReadAllText(path)) ?? throw new AssertionException("Manifest is null."));
        edit(manifest);
        File.WriteAllText(path, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CopyRelayBundle(out string temporary)
    {
        temporary = Path.Combine(Path.GetTempPath(), "arch-linter-net-integrity-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(RepositoryRoot(), "relay");
        string root = Path.Combine(temporary, "relay");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        foreach (string sourceName in BadgeRelayBundleIntegrityValidator.SourceNames)
        {
            File.Copy(Path.Combine(source, "src", sourceName), Path.Combine(root, "src", sourceName));
        }

        foreach (string fileName in new[] { "package.json", "package-lock.json", "tsconfig.json", "wrangler.jsonc", "THIRD-PARTY-NOTICES.txt", "bundle-manifest.json" })
        {
            File.Copy(Path.Combine(source, fileName), Path.Combine(root, fileName));
        }

        string schemaDirectory = Path.Combine(temporary, "schema", "0.8.0");
        Directory.CreateDirectory(schemaDirectory);
        File.Copy(
            Path.Combine(RepositoryRoot(), "schema", "0.8.0", "badge-relay-config.schema.json"),
            Path.Combine(schemaDirectory, "badge-relay-config.schema.json"));
        return root;
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? current = new(TestContext.CurrentContext.TestDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ArchLinterNet.slnx"))) return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static BadgeSetupConfiguration Configuration() => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.Relay.ToWireValue(),
        BadgeSetupContract.HeadlinePlusFreshnessProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private", 123, 456),
        new("a7f4k2m9", "0123456789abcdef0123456789abcdef", "https://relay.example", "architecture-health-badge-relay/a7f4k2m9"),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        DisclosureApproved: true,
        ProviderPlan: "pro");
}
