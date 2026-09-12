using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

internal abstract class CliIntegrationTestBase
{
    protected static string RepoRoot = null!;
    protected static string CliDllPath = null!;
    protected static string PassingPolicy = null!;
    protected static string FailingPolicy = null!;
    protected static string CoveragePolicyPath = null!;
    protected static string AllCoverageScopesPolicy = null!;
    protected static string GraphPolicy = null!;
    protected static string PassingWithIdsPolicy = null!;
    protected static string CombinedEquivalencePolicy = null!;

    [OneTimeSetUp]
    protected void OneTimeSetUp()
    {
        RepoRoot = FindRepoRoot();
        CliDllPath = Path.Combine(AppContext.BaseDirectory, "ArchLinterNet.Cli.dll");
        PassingPolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-policy.yml");
        FailingPolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "failing-policy.yml");
        CoveragePolicyPath = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "coverage-policy.yml");
        AllCoverageScopesPolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "all-coverage-scopes-policy.yml");
        GraphPolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "graph-policy.yml");
        PassingWithIdsPolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        CombinedEquivalencePolicy = Path.Combine(
            RepoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "combined-equivalence-policy.yml");

        if (!File.Exists(CliDllPath))
        {
            throw new InvalidOperationException(
                $"CLI artifact was not built by the test project: {CliDllPath}");
        }
    }

    protected static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && dir.GetFiles("ArchLinterNet.slnx").Length == 0)
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    protected static (int ExitCode, string StdOut, string StdErr) RunCli(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot
        };

        startInfo.ArgumentList.Add(CliDllPath);
        foreach (string argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    protected static void AssertCliResultEquals(
        (int ExitCode, string StdOut, string StdErr) expected,
        (int ExitCode, string StdOut, string StdErr) actual)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.ExitCode, Is.EqualTo(expected.ExitCode));
            Assert.That(actual.StdOut, Is.EqualTo(expected.StdOut));
            Assert.That(actual.StdErr, Is.EqualTo(expected.StdErr));
        });
    }

    protected static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

}
