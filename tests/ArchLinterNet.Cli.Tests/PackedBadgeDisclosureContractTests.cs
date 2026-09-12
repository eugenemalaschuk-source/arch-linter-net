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
                "/p:PackageVersion=" + PackageVersion));
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
                    setupPrefix + "relay/wrangler.jsonc",
                })
                {
                    Assert.That(package.GetEntry(asset), Is.Not.Null, $"Missing packed setup asset '{asset}'.");
                }
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
