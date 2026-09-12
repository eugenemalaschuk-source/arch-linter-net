using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

internal abstract class CliIntegrationTestBase
{
    protected static string _repoRoot = null!;
    protected static string _cliDllPath = null!;
    protected static string _passingPolicy = null!;
    protected static string _failingPolicy = null!;
    protected static string _coveragePolicy = null!;
    protected static string _allCoverageScopesPolicy = null!;
    protected static string _graphPolicy = null!;
    protected static string _passingWithIdsPolicy = null!;
    protected static string _combinedEquivalencePolicy = null!;

    [OneTimeSetUp]
    protected void OneTimeSetUp()
    {
        _repoRoot = FindRepoRoot();
        _cliDllPath = Path.Combine(AppContext.BaseDirectory, "ArchLinterNet.Cli.dll");
        _passingPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-policy.yml");
        _failingPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "failing-policy.yml");
        _coveragePolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "coverage-policy.yml");
        _allCoverageScopesPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "all-coverage-scopes-policy.yml");
        _graphPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "graph-policy.yml");
        _passingWithIdsPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        _combinedEquivalencePolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "combined-equivalence-policy.yml");

        if (!File.Exists(_cliDllPath))
        {
            throw new InvalidOperationException(
                $"CLI artifact was not built by the test project: {_cliDllPath}");
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
            WorkingDirectory = _repoRoot
        };

        startInfo.ArgumentList.Add(_cliDllPath);
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
