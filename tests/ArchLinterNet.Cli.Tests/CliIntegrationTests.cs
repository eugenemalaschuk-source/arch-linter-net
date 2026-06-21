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
                Arguments = $"build \"{_cliProjectPath}\" --nologo --verbosity quiet",
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

    /* --condition-set */

    [Test]
    public void UnknownConditionSet_ExitsTwoWithDiagnostic()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict", "--condition-set", "nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Unknown condition set"));
            Assert.That(stderr, Does.Contain("nonexistent"));
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

    /* --timings flag */

    [Test]
    public void Timings_PrintsPhaseNamesToStderr()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict", "--timings");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Does.Contain("Validation timings:"));
            Assert.That(stderr, Does.Contain("total"));
            Assert.That(stderr, Does.Contain("load_and_setup"));
            Assert.That(stderr, Does.Contain("configuration_check"));
            Assert.That(stderr, Does.Contain("contract_checks"));
            Assert.That(stderr, Does.Contain("ms"));
        });
    }

    [Test]
    public void Timings_ExitCodeMatchesNonTimings()
    {
        var (normalExit, _, _) = RunCli("--policy", _passingPolicy, "--strict");
        var (timingExit, _, _) = RunCli("--policy", _passingPolicy, "--strict", "--timings");

        Assert.That(timingExit, Is.EqualTo(normalExit));
    }

    [Test]
    public void Timings_WithJson_StdoutRemainsValidJson()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--json", "--timings");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Does.Contain("Validation timings:"));
            Assert.DoesNotThrow(() => JsonDocument.Parse(stdout));
        });
    }

    [Test]
    public void Timings_StdoutUnchanged()
    {
        var (normalExit, normalOut, normalErr) = RunCli("--policy", _passingPolicy, "--strict");
        var (timingExit, timingOut, timingErr) = RunCli("--policy", _passingPolicy, "--strict", "--timings");

        Assert.Multiple(() =>
        {
            Assert.That(timingExit, Is.EqualTo(normalExit));
            Assert.That(timingOut, Is.EqualTo(normalOut));
            Assert.That(timingErr, Is.Not.Empty);
            Assert.That(normalErr, Is.Empty);
        });
    }

    [Test]
    public void Timings_WithAudit_PrintsPhaseNames()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--audit", "--timings");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Does.Contain("Validation timings:"));
            Assert.That(stderr, Does.Contain("contract_checks"));
            Assert.That(stderr, Does.Contain("ms"));
        });
    }

    [Test]
    public void Timings_WithoutFlag_NoStderrOutput()
    {
        var (_, _, stderr) = RunCli("--policy", _passingPolicy, "--strict");

        Assert.That(stderr, Is.Empty);
    }

    /* baseline generate */

    [Test]
    public void BaselineGenerate_WithValidPolicy_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True, "Output file should exist");
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });

            string content = File.ReadAllText(outputPath);
            Assert.That(content, Does.Contain("version: 1"));
            Assert.That(content, Does.Contain("baseline:"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithReason_Succeeds()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--reason", "my custom reason");

            Assert.That(exitCode, Is.EqualTo(0), $"Baseline generate failed, stderr: {stderr}");
            Assert.That(File.Exists(outputPath), Is.True);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_MissingOutput_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("baseline", "generate",
            "--config", _passingPolicy);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("--output"));
        });
    }

    [Test]
    public void BaselineGenerate_InvalidConfig_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", "/nonexistent/policy.yml",
                "--output", outputPath);

            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineHelp_ShowsUsage()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("baseline generate"));
            Assert.That(stdout, Does.Contain("--config"));
            Assert.That(stdout, Does.Contain("--output"));
        });
    }

    /* validate --baseline */

    [Test]
    public void ValidateWithBaseline_EmptyBaseline_StillPasses()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (genExit, _, _) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", baselinePath);
            Assert.That(genExit, Is.EqualTo(0), "Baseline generation should succeed");

            var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--strict",
                "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0),
                    $"Validate with baseline should pass, stderr: {stderr}");
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void ValidateWithBaseline_UnknownContractId_ExitsTwo()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"bad-baseline-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(baselinePath, @"
version: 1
baseline:
  strict:
    - id: nonexistent-contract
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: stale
");

            var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict",
                "--baseline", baselinePath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("unknown contract"));
                Assert.That(stderr, Does.Contain("nonexistent-contract"));
            });
        }
        finally
        {
            if (File.Exists(baselinePath))
                File.Delete(baselinePath);
        }
    }

    [Test]
    public void ValidateWithBaseline_MissingBaselineFile_ExitsTwo()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--strict",
            "--baseline", "/nonexistent/baseline.yml");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("not found"));
        });
    }

    /* baseline generate --mode */

    [Test]
    public void BaselineGenerate_StrictMode_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "strict");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline strict mode failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_AuditMode_ProducesValidFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, stdout, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "audit");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), $"Baseline audit mode failed, stderr: {stderr}");
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(stdout, Does.Contain("Generated baseline"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_InvalidMode_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--mode", "invalid");

            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Does.Contain("Invalid mode"));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithConditionSet_FlagParsed()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _passingPolicy,
                "--output", outputPath,
                "--condition-set", "nonexistent");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("condition set"));
                Assert.That(stderr, Does.Contain("nonexistent"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Test]
    public void BaselineGenerate_WithInvalidPolicy_ExitsTwo()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        try
        {
            var (exitCode, _, stderr) = RunCli("baseline", "generate",
                "--config", _failingPolicy,
                "--output", outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(2));
                Assert.That(stderr, Does.Contain("Configuration violations"));
                Assert.That(stderr, Does.Contain("missing target assembly"));
            });
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    /* baseline --help */

    [Test]
    public void BaselineHelp_ShowsModeOption()
    {
        var (exitCode, stdout, _) = RunCli("baseline", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("--mode"));
            Assert.That(stdout, Does.Contain("strict, audit, or all"));
            Assert.That(stdout, Does.Contain("--condition-set"));
        });
    }
}
