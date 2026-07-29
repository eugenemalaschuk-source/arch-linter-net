using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CheckpointACommandLineAcceptanceTests
{
    [Test]
    public void RedirectedHumanJsonAndSarif_UseOneCorpusScenario()
    {
        string repositoryRoot = FindRepositoryRoot();
        string policy = Path.Combine(
            repositoryRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "imported-provenance-root.yml");
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-checkpoint-a-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string jsonPath = Path.Combine(directory, "result.json");
            string sarifPath = Path.Combine(directory, "result.sarif");
            var result = RunCli(repositoryRoot, policy, jsonPath, sarifPath);

            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(jsonPath));
            using JsonDocument sarif = JsonDocument.Parse(File.ReadAllText(sarifPath));
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1), result.StdErr);
                Assert.That(result.StdOut, Does.Contain("root: imported-provenance-root.yml"));
                Assert.That(result.StdOut, Does.Not.Contain("\u001b["));
                Assert.That(json.RootElement.GetProperty("violations")[0].GetProperty("policy_location")
                    .GetProperty("role").GetString(), Is.EqualTo("fragment"));
                Assert.That(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
                    .GetProperty("relatedLocations"), Is.Not.Empty);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(
        string repositoryRoot, string policy, string jsonPath, string sarifPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policy);
        startInfo.ArgumentList.Add("--strict");
        startInfo.ArgumentList.Add("--report");
        startInfo.ArgumentList.Add("human=stdout");
        startInfo.ArgumentList.Add("--report");
        startInfo.ArgumentList.Add($"json={jsonPath}");
        startInfo.ArgumentList.Add("--report");
        startInfo.ArgumentList.Add($"sarif={sarifPath}");

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && directory.GetFiles("ArchLinterNet.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
