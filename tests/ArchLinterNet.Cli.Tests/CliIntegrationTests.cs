using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public partial class CliIntegrationTests
{
    private static string _repoRoot = null!;
    private static string _cliDllPath = null!;
    private static string _passingPolicy = null!;
    private static string _failingPolicy = null!;
    private static string _coveragePolicy = null!;
    private static string _allCoverageScopesPolicy = null!;
    private static string _graphPolicy = null!;
    private static string _passingWithIdsPolicy = null!;
    private static string _combinedEquivalencePolicy = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
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

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && dir.GetFiles("ArchLinterNet.slnx").Length == 0)
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(params string[] args)
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

    [TestCase("--help", "debt", "Unknown command or argument: debt")]
    [TestCase("-h", "debt", "Unknown command or argument: debt")]
    [TestCase("--version", "debt", "Unknown command or argument: debt")]
    [TestCase("-v", "debt", "Unknown command or argument: debt")]
    [TestCase("--help", "--bogus-flag", "Unknown option: --bogus-flag")]
    public void HelpOrVersionFollowedByInvalidInput_FailsClosed(
        string legacyOption,
        string invalidInput,
        string expectedDiagnostic)
    {
        var (exitCode, stdout, stderr) = RunCli(legacyOption, invalidInput);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain(expectedDiagnostic));
            Assert.That(stderr, Does.Contain("--help"));
        });
    }

    [Test]
    public void ValidFlagThenHelpFollowedByInvalidInput_FailsClosed()
    {
        var (exitCode, stdout, stderr) = RunCli("--strict", "--help", "debt");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("Unknown command or argument: debt"));
            Assert.That(stderr, Does.Contain("--help"));
        });
    }

    [Test]
    public void PolicyArgumentThenHelpFollowedByInvalidInput_FailsClosed()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", "x", "--help", "debt");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("Unknown command or argument: debt"));
            Assert.That(stderr, Does.Contain("--help"));
        });
    }

    [Test]
    public void UnknownTopLevelCommand_FailsClosedWithoutRootHelp()
    {
        var (exitCode, stdout, stderr) = RunCli("debt", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("Unknown command or argument: debt"));
            Assert.That(stderr, Does.Contain("--help"));
        });
    }

    [Test]
    public void UnknownSubcommand_FailsClosedWithoutParentHelp()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "debt", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("Unknown command or argument: debt"));
            Assert.That(stderr, Does.Contain("--help"));
        });
    }

    [Test]
    public void ValidSubcommandHelp_ExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("policy", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("policy"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void DotnetProcessArguments_PreserveWhitespaceAndEmbeddedQuotes()
    {
        const string ComplexArgument = "path with spaces/\"quoted\".yml";
        var startInfo = new ProcessStartInfo("dotnet");

        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(ComplexArgument);

        Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "--policy", ComplexArgument }));
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

    [Test]
    public void Version_WithAdditionalRootArguments_StillPrintsVersionAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--version");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("arch-linter-net"));
            Assert.That(stderr, Is.Empty);
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

    /* --mode strict,audit (issue #363: one snapshot serves every requested mode) */

    [Test]
    public void CombinedMode_ExitsZeroWhenBothModesPass()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--mode", "strict,audit");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");
            Assert.That(stdout, Does.Contain("passed"));
        });
    }

    [Test]
    public void CombinedMode_FailsWhenEitherRequestedModeFails()
    {
        var (combinedExit, _, _) = RunCli("--policy", _failingPolicy, "--mode", "strict,audit");
        var (strictExit, _, _) = RunCli("--policy", _failingPolicy, "--mode", "strict");

        Assert.That(combinedExit, Is.EqualTo(strictExit).And.Not.EqualTo(0));
    }

    [Test]
    public void CombinedMode_InvalidModeInList_ReportsError()
    {
        var (exitCode, _, stderr) = RunCli("--policy", _passingPolicy, "--mode", "strict,bogus");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Not.EqualTo(0));
            Assert.That(stderr, Does.Contain("Invalid mode"));
        });
    }

    // Regression test for a PR #390 review defect: --format json for a combined multi-mode run
    // used to write each mode's JSON object on stdout back-to-back, producing two concatenated
    // root objects — not something a normal JSON parser can consume as one document. Parsing
    // stdout as a single JSON document (and finding one "results" entry per requested mode) is the
    // actual regression check; the earlier combined-mode tests only checked exit code and a
    // substring, which would not have caught this.
    [Test]
    public void CombinedMode_JsonFormat_ProducesOneParseableDocumentWithOneResultPerMode()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--mode", "strict,audit", "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(results.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(results.GetArrayLength(), Is.EqualTo(2));
        });
    }

    // Same defect as above, for --format sarif: two concatenated SARIF documents on stdout instead
    // of one. SARIF natively supports multiple runs in one document, so the fix merges each mode's
    // "runs" entries into one { "version", "runs": [...] } document instead of emitting one
    // document per mode.
    [Test]
    public void CombinedMode_SarifFormat_ProducesOneParseableDocumentWithOneRunPerMode()
    {
        var (exitCode, stdout, stderr) = RunCli("--policy", _passingPolicy, "--mode", "strict,audit", "--format", "sarif");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement runs = document.RootElement.GetProperty("runs");

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            Assert.That(runs.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(runs.GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public void ValidateModeFlags_RespectLeftToRightPrecedence()
    {
        var strictFromTail = RunCli("--policy", _failingPolicy, "--audit", "--mode", "strict");
        var strictCanonical = RunCli("--policy", _failingPolicy, "--mode", "strict");
        var auditFromTail = RunCli("--policy", _failingPolicy, "--mode", "strict", "--audit");
        var auditCanonical = RunCli("--policy", _failingPolicy, "--mode", "audit");

        AssertCliResultEquals(strictCanonical, strictFromTail);
        AssertCliResultEquals(auditCanonical, auditFromTail);
    }

    [Test]
    public void ValidateModeShortcuts_RespectLeftToRightPrecedence()
    {
        var auditFromTail = RunCli("--policy", _failingPolicy, "--strict", "--audit");
        var auditCanonical = RunCli("--policy", _failingPolicy, "--mode", "audit");
        var strictFromTail = RunCli("--policy", _failingPolicy, "--audit", "--strict");
        var strictCanonical = RunCli("--policy", _failingPolicy, "--mode", "strict");

        AssertCliResultEquals(auditCanonical, auditFromTail);
        AssertCliResultEquals(strictCanonical, strictFromTail);
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

    [Test]
    public void ValidateFormatFlags_RespectLeftToRightPrecedence()
    {
        var sarifFromTail = RunCli("--policy", _failingPolicy, "--json", "--format", "sarif", "--strict");
        var sarifCanonical = RunCli("--policy", _failingPolicy, "--format", "sarif", "--strict");
        var jsonFromTail = RunCli("--policy", _failingPolicy, "--format", "sarif", "--json", "--strict");
        var jsonCanonical = RunCli("--policy", _failingPolicy, "--format", "json", "--strict");

        AssertCliResultEquals(sarifCanonical, sarifFromTail);
        AssertCliResultEquals(jsonCanonical, jsonFromTail);
    }

    /* --format sarif */

    [Test]
    public void SarifOutput_IsValidSarifWithExpectedSchema()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _passingPolicy, "--format", "sarif");

        Assert.That(exitCode, Is.EqualTo(0));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            Assert.That(root.TryGetProperty("$schema", out _), Is.True);
            JsonElement run = root.GetProperty("runs")[0];
            Assert.That(run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString(),
                Is.EqualTo("arch-linter-net"));
            Assert.That(run.TryGetProperty("results", out _), Is.True);
        });
    }

    [Test]
    public void SarifOutput_WithViolations_IncludesResultWithNormalizedRuleId()
    {
        var (exitCode, stdout, _) = RunCli("--policy", _failingPolicy, "--format", "sarif", "--strict");

        Assert.That(exitCode, Is.EqualTo(1));

        using var doc = JsonDocument.Parse(stdout);
        JsonElement results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(results.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(results[0].GetProperty("ruleId").GetString(), Is.EqualTo("configuration"));
            Assert.That(results[0].GetProperty("level").GetString(), Is.EqualTo("error"));
        });
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
        var (exitCode, stdout, stderr) = RunCli("--bogus-flag");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("--bogus-flag"));
            Assert.That(stderr, Does.Contain("--help"));
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

    private static void AssertCliResultEquals(
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

}
