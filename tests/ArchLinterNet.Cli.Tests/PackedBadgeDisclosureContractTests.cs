using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
[NonParallelizable]
[Category("E2E")]
public sealed class PackedBadgeDisclosureContractTests
{
    [Test]
    public void FreshlyPackedTool_ContainsAndVerifiesTheClosedDisclosureContract()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), "arch-linter-net-827-" + Guid.NewGuid().ToString("N"));
        string feed = Path.Combine(temporary, "feed");
        string toolPath = Path.Combine(temporary, "tool");
        string extracted = Path.Combine(temporary, "fixtures");
        Directory.CreateDirectory(feed);
        Directory.CreateDirectory(toolPath);
        Directory.CreateDirectory(extracted);
        try
        {
            const string PackageVersion = "0.1.0-issue827";
            AssertSuccess(Run("dotnet", root,
                "pack", "src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj", "--no-restore", "--output", feed,
                "/p:PackageVersion=" + PackageVersion, "/p:UseSharedCompilation=false", "/p:NodeReuse=false", "--disable-build-servers"));
            string packagePath = Directory.EnumerateFiles(feed, "ArchLinterNet.Cli.*.nupkg").Single();

            string prefix = "tools/net10.0/any/architecture-health-badge-relay/";
            string setupPrefix = "tools/net10.0/any/architecture-health-badge-setup/";
            string[] requiredAssets =
            [
                "README.md",
                "canonical-freshness.json",
                "canonical-ready.json",
                "canonical-unavailable.json",
                "conformance-vectors.json",
                "reference-config.json",
                "relay-fixtures.schema.json",
            ];
            using (ZipArchive package = ZipFile.OpenRead(packagePath))
            {
                foreach (string asset in requiredAssets)
                {
                    ZipArchiveEntry? entry = package.GetEntry(prefix + asset);
                    Assert.That(entry, Is.Not.Null, $"Missing packed contract asset '{asset}'.");
                    entry!.ExtractToFile(Path.Combine(extracted, asset));
                }

                foreach (string asset in new[]
                {
                    setupPrefix + "schema/0.8.0/badge-relay-config.schema.json",
                    setupPrefix + "relay/src/index.ts",
                    setupPrefix + "relay/package.json",
                    setupPrefix + "relay/package-lock.json",
                    setupPrefix + "relay/THIRD-PARTY-NOTICES.txt",
                    setupPrefix + "relay/bundle-manifest.json",
                    setupPrefix + "relay/wrangler.jsonc",
                })
                {
                    Assert.That(package.GetEntry(asset), Is.Not.Null, $"Missing packed setup asset '{asset}'.");
                }

                using Stream manifestStream = package.GetEntry(setupPrefix + "relay/bundle-manifest.json")!.Open();
                using JsonDocument manifest = JsonDocument.Parse(manifestStream);
                Assert.Multiple(() =>
                {
                    Assert.That(manifest.RootElement.GetProperty("schema_id").GetString(), Is.EqualTo("badge-relay-bundle-manifest/v1"));
                    Assert.That(manifest.RootElement.GetProperty("bundle").GetString(), Is.EqualTo("badge-relay/v1"));
                    Assert.That(manifest.RootElement.GetProperty("compatibility_plan").GetString(), Is.EqualTo("architecture-health-badge-relay/v1"));
                    Assert.That(manifest.RootElement.GetProperty("files").GetArrayLength(), Is.EqualTo(15));
                    Assert.That(manifest.RootElement.GetProperty("files").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()), Does.Contain("schema/0.8.0/badge-relay-config.schema.json"));
                    Assert.That(manifest.RootElement.GetProperty("publisher_pins").GetProperty("commit").GetString(), Is.EqualTo("36c88c88cca708c10ade98ac1f1fee8c56c1cb30"));
                });
            }

            AssertSuccess(Run("dotnet", root,
                "tool", "install", "ArchLinterNet.Cli", "--tool-path", toolPath, "--add-source", feed,
                "--ignore-failed-sources", "--version", PackageVersion));

            string acceptedPayload = ReadCanonicalBytes(Path.Combine(extracted, "canonical-ready.json"));
            string acceptedInput = Path.Combine(extracted, "accepted.json");
            File.WriteAllText(acceptedInput, acceptedPayload);

            string installedTool = Path.Combine(toolPath, OperatingSystem.IsWindows() ? "arch-linter-net.exe" : "arch-linter-net");
            CommandResult accepted = Run(installedTool, root,
                "badge", "architecture-health", "--verify-disclosure-profile", "--disclosure-profile", "headline-only/v1",
                "--input", acceptedInput);
            Assert.Multiple(() =>
            {
                Assert.That(accepted.ExitCode, Is.EqualTo(0), accepted.Output + accepted.Error);
                Assert.That(accepted.Output, Does.Contain("\"valid\":true"));
            });

            string unavailableInput = Path.Combine(extracted, "unavailable.json");
            File.WriteAllText(unavailableInput, ReadCanonicalBytes(Path.Combine(extracted, "canonical-unavailable.json")));
            CommandResult unavailable = Run(installedTool, root,
                "badge", "architecture-health", "--verify-disclosure-profile", "--disclosure-profile", "headline-only/v1",
                "--input", unavailableInput);
            Assert.Multiple(() =>
            {
                Assert.That(unavailable.ExitCode, Is.EqualTo(0), unavailable.Output + unavailable.Error);
                Assert.That(unavailable.Output, Does.Contain("\"valid\":true"));
            });

            string altered = Path.Combine(extracted, "noncanonical.json");
            File.AppendAllText(altered, acceptedPayload + " ");
            CommandResult rejected = Run(installedTool, root,
                "badge", "architecture-health", "--verify-disclosure-profile", "--disclosure-profile", "headline-only/v1",
                "--input", altered);
            Assert.Multiple(() =>
            {
                Assert.That(rejected.ExitCode, Is.EqualTo(2), rejected.Output + rejected.Error);
                Assert.That(rejected.Output, Does.Contain("\"valid\":false"));
            });

            CommandResult setup = Run(installedTool, root,
                "badge", "architecture-health", "setup", "--repository", "synthetic-owner/synthetic-repo",
                "--visibility", "private", "--dry-run");
            Assert.Multiple(() =>
            {
                Assert.That(setup.ExitCode, Is.EqualTo(0), setup.Output + setup.Error);
                Assert.That(setup.Output, Does.Contain("\"Mode\":\"none\""));
            });

            string unsignedEvidence = Path.Combine(extracted, "unsigned-capabilities.json");
            File.WriteAllText(unsignedEvidence, JsonSerializer.Serialize(new
            {
                schema_id = "badge-relay-capability-evidence/v1",
                source = "live-github-provider-inspector/v1",
                observed_at = DateTimeOffset.UtcNow.ToString("O"),
                repository = "consumer-owner/consumer-repo",
                repository_id = 123456L,
                repository_owner_id = 654321L,
                visibility = "private",
                base_ref = "main",
                provider_plan = "pro",
                provider_account = "0123456789abcdef0123456789abcdef",
                required_check = true,
                rules_api = true,
                oidc = true,
                provider_quota = true,
                relay = true,
            }));
            string rejectedRelay = Path.Combine(temporary, "rejected-relay");
            CommandResult rejectedEvidenceSetup = Run(installedTool, root,
                "badge", "architecture-health", "setup",
                "--repository", "consumer-owner/consumer-repo",
                "--visibility", "private", "--mode", "relay",
                "--disclosure-profile", "headline-plus-freshness/v1",
                "--repository-id", "123456", "--repository-owner-id", "654321",
                "--account", "0123456789abcdef0123456789abcdef", "--alias", "a7f4k2m9",
                "--endpoint", "https://relay.example", "--audience", "consumer-badge/relay",
                "--provider-plan", "pro", "--capability-evidence", unsignedEvidence,
                "--approve-disclosure", "--renewal", "--cadence-minutes", "30",
                "--output", rejectedRelay);
            Assert.Multiple(() =>
            {
                Assert.That(rejectedEvidenceSetup.ExitCode, Is.EqualTo(2), rejectedEvidenceSetup.Output + rejectedEvidenceSetup.Error);
                Assert.That(rejectedEvidenceSetup.Output, Does.Contain("invalid-observation"));
                Assert.That(Directory.Exists(rejectedRelay), Is.False);
            });

            string generated = Path.Combine(temporary, "generated-none");
            CommandResult generatedSetup = Run(installedTool, root,
                "badge", "architecture-health", "setup",
                "--repository", "consumer-owner/consumer-repo",
                "--visibility", "private", "--mode", "none",
                "--output", generated);
            string generatedConfig = Path.Combine(generated, "badge-relay-config.json");
            string generatedProducerPath = Path.Combine(generated, ".github", "workflows", "architecture-health-badge-producer.yml");
            string generatedProducer = File.Exists(generatedProducerPath) ? File.ReadAllText(generatedProducerPath) : string.Empty;
            Assert.Multiple(() =>
            {
                Assert.That(generatedSetup.ExitCode, Is.EqualTo(0), generatedSetup.Output + generatedSetup.Error);
                Assert.That(File.Exists(generatedConfig), Is.True);
                Assert.That(generatedProducer, Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(generated, "relay", "wrangler.jsonc")), Is.False);
            });
            CommandResult healthyDoctor = Run(installedTool, root,
                "badge", "architecture-health", "doctor", "--input", generatedConfig);
            Assert.Multiple(() =>
            {
                Assert.That(healthyDoctor.ExitCode, Is.EqualTo(0), healthyDoctor.Output + healthyDoctor.Error);
                Assert.That(healthyDoctor.Output, Does.Contain("\"Available\":true"));
            });
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ArchLinterNet.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string ReadCanonicalBytes(string fixturePath)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        return fixture.RootElement.GetProperty("canonical_bytes").GetString()
            ?? throw new InvalidOperationException($"The packaged fixture '{Path.GetFileName(fixturePath)}' has no canonical bytes.");
    }

    private static CommandResult Run(string fileName, string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new CommandResult(
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    private static void AssertSuccess(CommandResult result) =>
        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output + result.Error);

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
