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
            const string version = "0.1.0-issue827";
            AssertSuccess(Run("dotnet", root,
                "pack", "src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj", "--no-restore", "--output", feed,
                "/p:PackageVersion=" + version));
            string packagePath = Directory.EnumerateFiles(feed, "ArchLinterNet.Cli.*.nupkg").Single();

            string prefix = "tools/net10.0/any/architecture-health-badge-relay/";
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
            }

            AssertSuccess(Run("dotnet", root,
                "tool", "install", "ArchLinterNet.Cli", "--tool-path", toolPath, "--add-source", feed,
                "--ignore-failed-sources", "--version", version));

            string readyFixture = Path.Combine(extracted, "canonical-ready.json");
            using JsonDocument ready = JsonDocument.Parse(File.ReadAllText(readyFixture));
            string acceptedPayload = ready.RootElement.GetProperty("canonical_bytes").GetString()
                ?? throw new InvalidOperationException("The packaged ready fixture has no canonical bytes.");
            string acceptedInput = Path.Combine(extracted, "accepted.json");
            File.WriteAllText(acceptedInput, acceptedPayload);

            string installedTool = Path.Combine(toolPath, "arch-linter-net.exe");
            CommandResult accepted = Run(installedTool, root,
                "badge", "architecture-health", "--verify-disclosure-profile", "--disclosure-profile", "headline-only/v1",
                "--input", acceptedInput);
            Assert.Multiple(() =>
            {
                Assert.That(accepted.ExitCode, Is.EqualTo(0), accepted.Output + accepted.Error);
                Assert.That(accepted.Output, Does.Contain("\"valid\":true"));
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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, output, error);
    }

    private static void AssertSuccess(CommandResult result) =>
        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output + result.Error);

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
