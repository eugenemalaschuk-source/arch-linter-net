using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Badge.Application;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class BadgeCommandHandlerTests
{
    [TestCase("{\"mode\":\"strict\",\"passed\":true}", CliExitCodes.Success, "passing")]
    [TestCase("{\"mode\":\"strict\",\"passed\":false}", CliExitCodes.ValidationFailure, "failing")]
    [TestCase("{\"mode\":\"audit\",\"passed\":true}", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    [TestCase("{\"mode\":\"strict\"}", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    [TestCase("{\"mode\":\"strict\",\"passed\":\"true\"}", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    [TestCase("not-json", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    [TestCase("{\"results\":[{\"mode\":\"strict\",\"passed\":true}]}", CliExitCodes.Success, "passing")]
    [TestCase("{\"results\":[{\"mode\":\"audit\",\"passed\":true}]}", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    public void Handler_ProjectsOnlyStrictInput(string input, int expectedExitCode, string expectedMessage)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input)).Execute(new("input.json", false));
        using JsonDocument output = JsonDocument.Parse(console.Output);
        Assert.Multiple(() => { Assert.That(exitCode, Is.EqualTo(expectedExitCode)); Assert.That(output.RootElement.GetProperty("message").GetString(), Is.EqualTo(expectedMessage)); });
    }

    [Test]
    public void Handler_ShowHelp_WritesUsageAndSucceeds()
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem("{}")).Execute(new("input.json", true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("arch-linter-net badge architecture-policy"));
        });
    }

    [Test]
    public void Handler_VerifiesClosedDisclosureBytesWithoutReprojectingHealth()
    {
        const string Payload = "{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\"}";
        FakeConsole console = new();

        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(Payload)).ExecuteArchitectureHealth(
            new("badge.json", null, false, "headline-only/v1", null, true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("b6a3501a87dc39495210674cfabdb19478df2cd7c701a05a0ea3c397d2166e3d"));
        });
    }

    [TestCase("healthy", "pass", 0, 42, CliExitCodes.Success, "PASS · HEALTHY · 0 ignores · 42 rules", "brightgreen")]
    [TestCase("debt", "pass", 7, 42, CliExitCodes.Success, "PASS · DEBT · 7 ignores · 42 rules", "yellow")]
    [TestCase("degrading", "pass", 8, 43, CliExitCodes.Success, "PASS · DEGRADING · 8 ignores · 43 rules", "orange")]
    [TestCase("degrading", "fail", 8, 43, CliExitCodes.ValidationFailure, "FAIL · DEGRADING · 8 ignores · 43 rules", "orange")]
    [TestCase("failing", "fail", 7, 42, CliExitCodes.ValidationFailure, "FAIL · FAILING · 7 ignores · 42 rules", "red")]
    [TestCase("unassessable", "unassessable", 7, 42, CliExitCodes.InvalidArgumentsOrRuntimeError, "UNASSESSABLE · ? ignores · ? rules", "lightgrey")]
    public void Handler_ProjectsCanonicalArchitectureHealth(
        string health,
        string gate,
        int ignores,
        int rules,
        int expectedExitCode,
        string expectedMessage,
        string expectedColor)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(Health(health, gate, ignores, rules)))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(expectedExitCode));
            Assert.That(output.RootElement.GetProperty("label").GetString(), Is.EqualTo("architecture"));
            Assert.That(output.RootElement.GetProperty("message").GetString(), Is.EqualTo(expectedMessage));
            Assert.That(output.RootElement.GetProperty("color").GetString(), Is.EqualTo(expectedColor));
        });
    }

    [TestCase("not-json")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[]}}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"unassessable\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[]}}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[{\"policy_inventory\":{\"schema\":\"architecture-policy-inventory/v1\",\"effective_rule_count\":42,\"ignore_debt\":{\"total\":7}}},{\"policy_inventory\":{\"schema\":\"architecture-policy-inventory/v1\",\"effective_rule_count\":43,\"ignore_debt\":{\"total\":7}}}]}}")]
    public void Handler_HealthInputUnavailable_UsesExplicitUnknownPayload(string input)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(output.RootElement.GetProperty("message").GetString(), Is.EqualTo("UNASSESSABLE · ? ignores · ? rules"));
            Assert.That(output.RootElement.GetProperty("color").GetString(), Is.EqualTo("lightgrey"));
        });
    }

    [TestCase("unknown", "pass")]
    [TestCase("healthy", "unknown")]
    public void Handler_HealthUnsupportedValues_UseExplicitUnknownPayload(string health, string gate)
    {
        string input = Health("healthy", "pass", 7, 42);
        if (health == "unknown")
        {
            input = input.Replace("\"health\":\"healthy\"", "\"health\":\"unknown\"", StringComparison.Ordinal);
        }
        if (gate == "unknown")
        {
            input = input.Replace("\"gate\":\"pass\"", "\"gate\":\"unknown\"", StringComparison.Ordinal);
        }
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(output.RootElement.GetProperty("message").GetString(),
                Is.EqualTo("UNASSESSABLE · ? ignores · ? rules"));
        });
    }

    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\"}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\"}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[{}]}}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[{\"policy_inventory\":{\"schema\":\"unexpected\",\"effective_rule_count\":42,\"ignore_debt\":{\"total\":7}}}]}}")]
    [TestCase("{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"report_evidence\":{\"validation_outcomes\":[{\"policy_inventory\":{\"schema\":\"architecture-policy-inventory/v1\",\"effective_rule_count\":-1,\"ignore_debt\":{\"total\":7}}}]}}")]
    public void Handler_HealthMalformedCanonicalFields_UseExplicitUnknownPayload(string input)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(output.RootElement.GetProperty("color").GetString(), Is.EqualTo("lightgrey"));
        });
    }

    [TestCase("\"schema_version\":2", "\"schema_version\":3")]
    [TestCase("\"kind\":\"architecture-health-report-evidence\"", "\"kind\":\"unsupported\"")]
    public void Handler_HealthRejectsUnsupportedOrMismatchedEvidenceEnvelope(string expected, string replacement)
    {
        string input = Health("healthy", "pass", 7, 42).Replace(expected, replacement, StringComparison.Ordinal);

        AssertUnassessable(input);
    }

    [Test]
    public void Handler_HealthRejectsMismatchedInnerEvidenceState()
    {
        JsonObject document = JsonNode.Parse(Health("healthy", "pass", 7, 42))!.AsObject();
        document["report_evidence"]!["health"] = "debt";

        AssertUnassessable(document.ToJsonString());
    }

    [Test]
    public void Handler_HealthRejectsInventoryLessProductionShapedOutcome()
    {
        JsonObject document = JsonNode.Parse(Health("healthy", "pass", 7, 42))!.AsObject();
        document["report_evidence"]!["validation_outcomes"]![0]!.AsObject().Remove("policy_inventory");

        AssertUnassessable(document.ToJsonString());
    }

    [Test]
    public void Handler_HealthRejectsNonObjectProductionShapedOutcome()
    {
        JsonObject document = JsonNode.Parse(Health("healthy", "pass", 7, 42))!.AsObject();
        document["report_evidence"]!["validation_outcomes"]![0] = null;

        AssertUnassessable(document.ToJsonString());
    }

    [Test]
    public void Handler_HealthReadFailure_WritesExplicitUnknownPayload()
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(
            "{}", readException: new IOException("input unavailable")))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(output.RootElement.GetProperty("message").GetString(),
                Is.EqualTo("UNASSESSABLE · ? ignores · ? rules"));
        });
    }

    [Test]
    public void Handler_HealthWriteFailure_ReturnsRuntimeError()
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(
            Health("healthy", "pass", 0, 42), writeException: new IOException("target unavailable")))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", "badge.json", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not write Architecture Health badge: target unavailable"));
        });
    }

    [Test]
    public void Handler_HealthOutputFile_IsDeterministic()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(Health("debt", "pass", 7, 42));
        BadgeCommandHandler handler = new(console, fileSystem);

        int first = handler.ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", "badge.json", false));
        string firstOutput = fileSystem.Written["badge.json"];
        int second = handler.ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", "badge.json", false));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(CliExitCodes.Success));
            Assert.That(second, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.Written["badge.json"], Is.EqualTo(firstOutput));
            Assert.That(console.Output, Is.Empty);
        });
    }

    [Test]
    public void Handler_HealthFreshnessProfile_UsesCanonicalEvidenceHorizon()
    {
        JsonObject document = JsonNode.Parse(Health("healthy", "pass", 0, 42))!.AsObject();
        document["report_evidence"]!["publication_evidence"] = new JsonObject
        {
            ["schema_id"] = "architecture-health-publication-evidence/v1",
            ["state"] = "ready",
            ["semantic_horizon"] = "2026-09-09T11:00:00Z",
            ["reasons"] = new JsonArray(),
        };
        FakeConsole console = new();

        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(document.ToJsonString()))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions(
                "input.json", null, false, "headline-plus-freshness/v1", "2026-09-09T10:30:00Z"));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(output.RootElement.GetProperty("verified_at").GetString(), Is.EqualTo("2026-09-09T10:30:00Z"));
            Assert.That(output.RootElement.GetProperty("valid_until").GetString(), Is.EqualTo("2026-09-09T11:00:00Z"));
            Assert.That(console.Output, Does.Contain("\\u00B7"));
        });
    }

    [Test]
    public void Handler_HealthProfile_RejectsLegacyEvidenceWithoutPublicationHorizon()
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(Health("healthy", "pass", 0, 42)))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions(
                "input.json", null, false, "headline-only/v1"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("UNASSESSABLE"));
            Assert.That(console.ErrorOutput, Does.Contain("Publication evidence is missing, legacy, or unsupported"));
        });
    }

    [Test]
    public void Handler_HealthProfile_ReportsUnsupportedProfileDiagnostic()
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(Health("healthy", "pass", 0, 42)))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions(
                "input.json", null, false, "unsupported/v1"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("UNASSESSABLE"));
            Assert.That(console.ErrorOutput, Does.Contain("Unsupported disclosure profile 'unsupported/v1'"));
        });
    }

    [Test]
    public void Handler_HealthProfile_RejectsCountsOutsideCanonicalBound()
    {
        JsonObject document = JsonNode.Parse(Health("healthy", "pass", 10_000, 42))!.AsObject();
        AddReadyPublicationEvidence(document);
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(document.ToJsonString()))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions(
                "input.json", null, false, "headline-only/v1"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("UNASSESSABLE"));
            Assert.That(console.ErrorOutput, Does.Contain("range 0 through 9999"));
        });
    }

    private static void AddReadyPublicationEvidence(JsonObject document)
    {
        document["report_evidence"]!["publication_evidence"] = new JsonObject
        {
            ["schema_id"] = "architecture-health-publication-evidence/v1",
            ["state"] = "ready",
            ["semantic_horizon"] = "2026-09-09T11:00:00Z",
            ["reasons"] = new JsonArray(),
        };
    }

    private static void AssertUnassessable(string input)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input))
            .ExecuteArchitectureHealth(new ArchitectureHealthBadgeCommandOptions("input.json", null, false));
        using JsonDocument output = JsonDocument.Parse(console.Output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(output.RootElement.GetProperty("message").GetString(),
                Is.EqualTo("UNASSESSABLE \u00B7 ? ignores \u00B7 ? rules"));
        });
    }

    private static string Health(string health, string gate, int ignores, int rules)
    {
        ArchitectureHealthGate gateState = gate switch
        {
            "pass" => ArchitectureHealthGate.Pass,
            "fail" => ArchitectureHealthGate.Fail,
            "unassessable" => ArchitectureHealthGate.Unassessable,
            _ => throw new ArgumentOutOfRangeException(nameof(gate)),
        };
        ArchitectureHealthState healthState = health switch
        {
            "healthy" => ArchitectureHealthState.Healthy,
            "debt" => ArchitectureHealthState.Debt,
            "degrading" => ArchitectureHealthState.Degrading,
            "failing" => ArchitectureHealthState.Failing,
            "unassessable" => ArchitectureHealthState.Unassessable,
            _ => throw new ArgumentOutOfRangeException(nameof(health)),
        };
        var inventory = new ArchitecturePolicyInventory(
            ArchitecturePolicyInventory.CurrentSchemaId,
            rules,
            new ArchitecturePolicyInventoryRules(rules, 0, 0),
            new ArchitecturePolicyInventoryIgnoreDebt(ignores, ignores, 0, 0, 0, 0),
            []);
        ValidationOutcome validation = new(
            Passed: gateState == ArchitectureHealthGate.Pass,
            Violations: [],
            Cycles: [],
            CoverageFindings: [],
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: [],
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: [],
            PolicyConsistencyConfig: "off",
            CoverageSummaries: [],
            ClassificationConflicts: [],
            ClassificationMetadataFailures: [])
        {
            PolicyInventory = inventory,
        };
        var debtGate = new ArchitectureDebtGateOutcome(
            Succeeded: true,
            Passed: gateState == ArchitectureHealthGate.Pass,
            new ArchitectureDebtGateEvaluation(true, "strict", []),
            new BaselineVerifyOutcome(true, true, [], [], [], [], []));
        var outcome = new ArchitectureHealthOutcome(
            new ArchitectureHealthSummary(
                ArchitectureHealthSummary.CurrentSchemaId,
                gateState,
                healthState,
                []),
            [new ArchitectureHealthValidationOutcome("strict", validation)],
            debtGate)
        {
            ExecutionContext = "badge-projector-test",
            ConditionSetName = "ci",
        };
        return ArchLinterNet.Core.Validation.ArchitectureHealthProjector.FormatAsJson(outcome);
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string Output => _output.ToString();
        public string ErrorOutput => _error.ToString();
    }

    private sealed class FakeFileSystem(string input, Exception? readException = null, Exception? writeException = null) : IFileSystem
    {
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        public bool FileExists(string path) => true;
        public string ReadAllText(string path) => readException is null ? input : throw readException;
        public byte[] ReadAllBytes(string path) => readException is null ? Encoding.UTF8.GetBytes(input) : throw readException;
        public void WriteAllText(string path, string contents) => Written[path] = contents;
        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            if (writeException is not null)
            {
                throw writeException;
            }

            string temporaryPath = targetPath + ".tmp";
            Written[temporaryPath] = contents;
            return temporaryPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath) => Written[targetPath] = Written[tempPath];
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
