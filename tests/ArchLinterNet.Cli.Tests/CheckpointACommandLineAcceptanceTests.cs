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
                Assert.That(
                    result.StdOut.IndexOf('\u001b'),
                    Is.EqualTo(-1),
                    Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(result.StdOut)));
                Assert.That(json.RootElement.GetProperty("violations")[0].GetProperty("policy_location")
                    .GetProperty("role").GetString(), Is.EqualTo("fragment"));
                Assert.That(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
                    .GetProperty("relatedLocations").GetArrayLength(), Is.GreaterThan(0));
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
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add(Path.Combine(
            repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll"));
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
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        return (process.ExitCode, outputTask.Result, errorTask.Result);
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
