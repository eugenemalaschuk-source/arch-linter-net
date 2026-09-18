using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeSetupHandoffTests
{
    [Test]
    public void Execute_HelpWritesApplyHandoffUsage()
    {
        RecordingConsole console = new();
        BadgeSetupHandoffCommandHandler handler = new(console);

        int exitCode = handler.Execute(new(null, null, null, null, null, null, null, null, ShowHelp: true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("apply-handoff"));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void Execute_InvalidInputWritesBoundedError()
    {
        RecordingConsole console = new();
        BadgeSetupHandoffCommandHandler handler = new(console);

        int exitCode = handler.Execute(new(null, null, null, null, null, null, null, null, ShowHelp: false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorText, Does.Contain("requires input"));
        });
    }

    [Test]
    public void Execute_ValidHandoffWritesSuccess()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        RecordingConsole console = new();
        BadgeSetupHandoffCommandHandler handler = new(console);

        int exitCode = handler.Execute(handoff.Options());

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("Verified bootstrap handoff applied"));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void Apply_ValidExactBaseHandoff_WritesOnlyVerifiedManagedFiles()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();

        BadgeSetupHandoff.Apply(handoff.Options());

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.EqualTo(handoff.Configuration));
            Assert.That(File.ReadAllText(Path.Combine(handoff.Consumer, "README.md")), Does.Contain(BadgeSetupOutputWriter.ReadmeStartMarker));
        });
    }

    [Test]
    public void Apply_TamperedPayload_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        File.WriteAllText(Path.Combine(handoff.Payload, "README.md"), "tampered\n", new UTF8Encoding(false));

        IOException exception = Assert.Throws<IOException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("digest"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "README.md")), Is.False);
        });
    }

    [Test]
    public void Apply_StaleReviewBranch_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        RunGit(handoff.Consumer, "commit", "--allow-empty", "-m", "stale branch");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("exact base commit and tree"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_ReviewBranchSubdirectory_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        string nestedDirectory = Path.Combine(handoff.Consumer, "nested");
        Directory.CreateDirectory(nestedDirectory);

        IOException exception = Assert.Throws<IOException>(() => BadgeSetupHandoff.Apply(handoff.Options() with { OutputDirectory = nestedDirectory }))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("target the root"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_MissingPayload_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();

        IOException exception = Assert.Throws<IOException>(() => BadgeSetupHandoff.Apply(handoff.Options() with { PayloadDirectory = Path.Combine(handoff.Root, "missing") }))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("payload directory is missing"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_MissingManifest_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();

        IOException exception = Assert.Throws<IOException>(() => BadgeSetupHandoff.Apply(handoff.Options() with { InputPath = Path.Combine(handoff.Root, "missing.json") }))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("manifest must be a regular file"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_UnsupportedSchema_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest["schema"] = "unsupported/v1";
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException exception = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("schema is unsupported"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_EmptyFileManifest_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest["files"]!.AsArray().Clear();
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException exception = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("must declare at least one"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_MissingRequiredManifestProperty_FailsBeforeAnyManagedWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest.Remove("base");
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException exception = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("missing a required property"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_WrongIdentityOrUnsafePath_FailsClosed()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();

        BadgeSetupHandoffCommandOptions wrongIdentity = handoff.Options() with { ExpectedRepositoryId = 999 };
        InvalidOperationException identityException = Assert.Throws<InvalidOperationException>(() => BadgeSetupHandoff.Apply(wrongIdentity))!;

        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest["files"]!.AsArray()[0]!.AsObject()["path"] = "../escape.txt";
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException pathException = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(identityException.Message, Does.Contain("base tree and repository identity"));
            Assert.That(pathException.Message, Does.Contain("unsafe or duplicate"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_UnapprovedManagedPath_FailsBeforeAnyWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest["files"]!.AsArray()[0]!.AsObject()["path"] = "tools/unapproved.md";
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException exception = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("unsafe or duplicate"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_UnknownManifestProperty_FailsBeforeAnyWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(handoff.ManifestPath))!.AsObject();
        manifest["unexpected"] = true;
        File.WriteAllText(handoff.ManifestPath, manifest.ToJsonString(), new UTF8Encoding(false));

        JsonException exception = Assert.Throws<JsonException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("duplicate or unknown"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    [Test]
    public void Apply_ReparsePointPayload_FailsBeforeAnyWrite()
    {
        using HandoffDirectory handoff = HandoffDirectory.Create();
        string readme = Path.Combine(handoff.Payload, "README.md");
        string target = Path.Combine(handoff.Root, "outside.md");
        File.WriteAllText(target, "outside\n", new UTF8Encoding(false));
        File.Delete(readme);
        try
        {
            File.CreateSymbolicLink(readme, target);
        }
        catch (Exception linkException) when (linkException is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Ignore("Symbolic link creation is not permitted or supported in this environment.");
        }

        IOException exception = Assert.Throws<IOException>(() => BadgeSetupHandoff.Apply(handoff.Options()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("symlink, junction, or reparse point"));
            Assert.That(File.Exists(Path.Combine(handoff.Consumer, "badge-relay-config.json")), Is.False);
        });
    }

    private sealed class HandoffDirectory : IDisposable
    {
        private HandoffDirectory(
            string root,
            string consumer,
            string payload,
            string manifestPath,
            string configuration,
            string baseSha,
            string baseTreeSha)
        {
            Root = root;
            Consumer = consumer;
            Payload = payload;
            ManifestPath = manifestPath;
            Configuration = configuration;
            BaseSha = baseSha;
            BaseTreeSha = baseTreeSha;
        }

        internal string Root { get; }
        internal string Consumer { get; }
        internal string Payload { get; }
        internal string ManifestPath { get; }
        internal string Configuration { get; }
        private string BaseSha { get; }
        private string BaseTreeSha { get; }

        internal static HandoffDirectory Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "arch-linter-net-handoff-" + Guid.NewGuid().ToString("N"));
            string consumer = Path.Combine(root, "consumer");
            string generated = Path.Combine(root, "generated");
            string payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(consumer);
            File.WriteAllText(Path.Combine(consumer, "base.txt"), "base\n", new UTF8Encoding(false));
            RunGit(consumer, "init");
            RunGit(consumer, "config", "user.name", "ArchLinterNet tests");
            RunGit(consumer, "config", "user.email", "tests@example.invalid");
            RunGit(consumer, "add", "base.txt");
            RunGit(consumer, "commit", "-m", "base");
            string baseSha = RunGit(consumer, "rev-parse", "HEAD");
            string baseTreeSha = RunGit(consumer, "rev-parse", "HEAD^{tree}");

            BadgeSetupConfiguration requested = ConfigurationModel();
            BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
                requested,
                new(
                    "owner",
                    "repo",
                    "private",
                    new(
                        HasRequiredCheck: true,
                        HasRulesApi: true,
                        CanUseOidc: true,
                        RepositoryId: 123,
                        RepositoryOwnerId: 456))).Plan;
            Assert.That(plan.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(generated, requested, plan);
            CopyDirectory(generated, payload);
            string configuration = File.ReadAllText(Path.Combine(payload, "badge-relay-config.json"));
            BadgeSetupConfiguration parsed = BadgeSetupConfigurationParser.Parse(configuration).Configuration!;

            string manifestPath = Path.Combine(root, "bootstrap-handoff.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
                schema = "architecture-health-badge-bootstrap-handoff/v1",
                @base = new { @ref = "main", sha = baseSha, tree_sha = baseTreeSha },
                repository = new { owner = "owner", name = "repo", visibility = "private", repository_id = 123L, repository_owner_id = 456L },
                publisher = new
                {
                    workflow_ref = parsed.Pins!.WorkflowRef,
                    workflow_sha = parsed.Pins.WorkflowSha,
                    action_ref = parsed.Pins.ActionRef,
                    bundle_digest = parsed.Pins.BundleDigest,
                },
                configuration = new
                {
                    schema_id = parsed.SchemaId,
                    contract_version = parsed.ContractVersion,
                    mode = parsed.Mode,
                    disclosure_profile = parsed.DisclosureProfile,
                },
                files = Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories)
                    .Select(path => new
                    {
                        path = Path.GetRelativePath(payload, path).Replace('\\', '/'),
                        bytes = new FileInfo(path).Length,
                        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    })
                    .OrderBy(static file => file.path, StringComparer.Ordinal),
            }), new UTF8Encoding(false));

            return new(root, consumer, payload, manifestPath, configuration, baseSha, baseTreeSha);
        }

        internal BadgeSetupHandoffCommandOptions Options() => new(
            ManifestPath,
            Payload,
            Consumer,
            BaseSha,
            BaseTreeSha,
            "owner/repo",
            123,
            456,
            ShowHelp: false);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                foreach (string directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories)
                    .OrderByDescending(static directory => directory.Length))
                {
                    File.SetAttributes(directory, FileAttributes.Normal);
                }
                Directory.Delete(Root, recursive: true);
            }
        }

        private static BadgeSetupConfiguration ConfigurationModel() => new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            BadgeSetupMode.None.ToWireValue(),
            BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new("owner", "repo", "private", 123, 456),
            new(null),
            new(false, 1440, 60),
            new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
            BaseRef: "main",
            Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
            DisclosureApproved: true);

        private static void CopyDirectory(string source, string destination)
        {
            foreach (string sourcePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetRelativePath(source, sourcePath));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(sourcePath, target);
            }
        }
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new IOException("Could not start Git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new IOException("Git command failed: " + error);
        }

        return output.Trim();
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        internal string Output => _output.ToString();
        internal string ErrorText => _error.ToString();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
    }
}
