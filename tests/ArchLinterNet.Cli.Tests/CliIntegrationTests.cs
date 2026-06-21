using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public class CliIntegrationTests
{
    private static string _repoRoot = null!;
    private static string _cliProjectPath = null!;
    private static string _passingPolicy = null!;
    private static string _failingPolicy = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _repoRoot = FindRepoRoot();
        _cliProjectPath = Path.Combine(_repoRoot, "src", "ArchLinterNet.Cli");
        _passingPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-policy.yml");
        _failingPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "failing-policy.yml");

        using var build = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{_cliProjectPath}\" --nologo -q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _repoRoot
            }
        };
        build.Start();

        string buildStderr = build.StandardError.ReadToEnd();
        build.WaitForExit();

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"CLI project build failed (exit {build.ExitCode}):{Environment.NewLine}{buildStderr}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !dir.GetFiles("ArchLinterNet.slnx").Any())
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(params string[] args)
    {
        string joinedArgs = args.Length > 0
            ? string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
            : string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{_cliProjectPath}\" -- {joinedArgs}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _repoRoot
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }

    /* --help / -h */

    [Test]
    public void Help_ShowsUsageAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("arch-linter-net"));
            Assert.That(stdout, Does.Contain("--help"));
            Assert.That(stdout, Does.Contain("--version"));
            Assert.That(stdout, Does.Contain("--policy"));
            Assert.That(stdout, Does.Contain("--mode"));
            Assert.That(stdout, Does.Contain("--format"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void HelpShortcut_ShowsUsageAndExitsZero()
    {
        var (exitCode, stdout, _) = RunCli("-h");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("arch-linter-net"));
        });
    }

    /* --version / -v */

    [Test]
    public void Version_PrintsVersionAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("--version");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("arch-linter-net"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void VersionShortcut_PrintsVersionAndExitsZero()
    {
        var (exitCode, stdout, _) = RunCli("-v");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("arch-linter-net"));
        });
    }

    /* --policy / -p */

    [Test]
    public void CustomPolicyPath_WithValidPath_ExitsZero()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict");

        Assert.That(exitCode, Is.EqualTo(0),
            $"Policy should pass, stderr: {stderr}");
    }

    [Test]
    public void PolicyShortcut_WithValidPath_ExitsZero()
    {
        var (exitCode, _, _) = RunCli("-p", _passingPolicy, "--strict");

        Assert.That(exitCode, Is.EqualTo(0));
    }

    [Test]
    public void MissingPolicyFile_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("--policy", "/nonexistent/path.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found").Or.Contains("No such file"));
        });
    }

    /* --mode strict / audit */

    [Test]
    public void StrictMode_ExitsZeroWhenPassing()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--mode", "strict");

        Assert.That(exitCode, Is.EqualTo(0),
            $"Strict should pass, stderr: {stderr}");
    }

    [Test]
    public void AuditMode_ExitsZeroWhenPassing()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--mode", "audit");

        Assert.That(exitCode, Is.EqualTo(0),
            $"Audit should pass, stderr: {stderr}");
    }

    [Test]
    public void AuditMode_ReportsDiagnostics()
    {
        var (_, stdout, _) = RunCli("--policy", _passingPolicy, "--mode", "audit");

        Assert.That(stdout, Does.Contain("passed").Or.Contain("violation").Or.Contain("cycle"));
    }

    [Test]
    public void StrictShortcut_WorksLikeModeStrict()
    {
        var (strictExit, _, _) = RunCli("--policy", _passingPolicy, "--strict");
        var (modeExit, _, _) = RunCli("--policy", _passingPolicy, "--mode", "strict");

        Assert.That(strictExit, Is.EqualTo(modeExit));
    }

    [Test]
    public void AuditShortcut_WorksLikeModeAudit()
    {
        var (auditExit, _, _) = RunCli("--policy", _passingPolicy, "--audit");
        var (modeExit, _, _) = RunCli("--policy", _passingPolicy, "--mode", "audit");

        Assert.That(auditExit, Is.EqualTo(modeExit));
    }

    /* --format human / json */

    [Test]
    public void HumanFormat_ProducesReadableOutput()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "human");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Not.Contain("\"passed\""));
        });
    }

    [Test]
    public void JsonOutput_IsValidJsonWithExpectedSchema()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.TryGetProperty("passed", out _), Is.True);
            Assert.That(root.TryGetProperty("mode", out _), Is.True);
            Assert.That(root.TryGetProperty("violations", out _), Is.True);
            Assert.That(root.TryGetProperty("cycles", out _), Is.True);
        });
    }

    [Test]
    public void JsonShortcut_ProducesValidJson()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.DoesNotThrow(() => JsonDocument.Parse(stdout));
    }

    /* Exit codes */

    [Test]
    public void StrictMode_WithViolations_ExitsOne()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _failingPolicy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void InvalidMode_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("--mode", "invalid");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Invalid mode"));
        });
    }

    [Test]
    public void UnknownFlag_ExitsWithError()
    {
        var (exitCode, _, stderr) = RunCli("--bogus-flag");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Unknown"));
        });
    }

    /* Invalid analysis config */

    [Test]
    public void InvalidUnmatchedIgnoredViolationsValue_ExitsWithError()
    {
        string invalidPolicy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "invalid-unmatched-config.yml");
        var (exitCode, _, stderr) = RunCli("--policy", invalidPolicy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("unmatched_ignored_violations"));
        });
    }

    /* --contract flag */

    [Test]
    public void SingleContract_SelectsMatchingContract()
    {
        string policyWithIds = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        var (exitCode, stdout, stderr) = RunCli("--policy", policyWithIds, "--strict", "--contract", "core-no-forbidden");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0),
                $"Policy should pass with --contract, stderr: {stderr}");
        });
    }

    [Test]
    public void MultipleContracts_SelectsAll()
    {
        string policyWithIds = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        var (exitCode, _, stderr) = RunCli("--policy", policyWithIds, "--strict",
            "--contract", "core-no-forbidden", "--contract", "no-non-existent");

        Assert.That(exitCode, Is.EqualTo(0),
            $"Policy should pass with two --contract flags, stderr: {stderr}");
    }

    [Test]
    public void SingleContract_WithAuditMode_SelectsAuditContract()
    {
        string policyWithIds = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        var (exitCode, _, stderr) = RunCli("--policy", policyWithIds, "--mode", "audit", "--contract", "audit-core-check");

        Assert.That(exitCode, Is.EqualTo(0),
            $"Should pass in audit mode with valid contract ID, stderr: {stderr}");
    }

    [Test]
    public void UnknownContractId_ExitsTwoWithDiagnostic()
    {
        string policyWithIds = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "passing-with-ids.yml");
        var (exitCode, _, stderr) = RunCli("--policy", policyWithIds, "--strict", "--contract", "nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Unknown contract ID"));
            Assert.That(stderr, Does.Contain("nonexistent"));
            Assert.That(stderr, Does.Contain("core-no-forbidden"));
            Assert.That(stderr, Does.Contain("no-non-existent"));
        });
    }

    [Test]
    public void UnknownExternalGroup_ReturnsValidationFailureInsteadOfRuntimeError()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "unknown-external-group.yml");
        var (exitCode, stdout, stderr) = RunCli("--policy", policy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stdout, Does.Contain("unknown external dependency group"));
            Assert.That(stderr, Is.Empty);
        });
    }

    /* Sample policy */

    [Test]
    public void SamplePolicy_DetectsMissingAssemblies()
    {
        string samplePolicy = Path.Combine(
            _repoRoot, "samples", "BasicCleanArchitecture", "architecture", "dependencies.arch.yml");
        var (exitCode, stdout, _) = RunCli("--policy", samplePolicy, "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stdout, Does.Contain("missing target assembly"));
        });
    }
}
