using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// End-to-end CLI wiring for --external-evidence/--evidence-*: real repository-local SARIF fixtures
// bound through ValidateCommandHandler, proving the CLI option surface reaches
// ArchitectureExternalEvidenceBinder and that its result correctly drives Human/JSON output and the
// existing PASS/FAIL/UNASSESSABLE -> 0/1/2 exit-code contract. Reuses FakeCliRuntime/FakeCliConsole/
// FakeFileSystem declared in ValidateCommandHandlerReportModeTests (partial class).
// See ArchitectureExternalEvidenceBinderTests (Core) for the underlying trust/selection/applicability
// behavior and ValidateCommandExternalEvidenceDefinitionTests (Cli) for option-syntax parsing.
public sealed partial class ValidateCommandHandlerReportModeTests
{
    [Test]
    public void ExternalEvidence_RequiredArtifactWithFindings_AppearsInJsonOutputAndPasses()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "json"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        using JsonDocument json = JsonDocument.Parse(console.StdOut);
        Assert.That(json.RootElement.GetProperty("imported_diagnostics").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public void ExternalEvidence_RequiredArtifactWithFindings_AppearsInSarifOutput()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "sarif"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        using JsonDocument sarif = JsonDocument.Parse(console.StdOut);
        JsonElement results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.That(results.EnumerateArray().Any(result =>
                result.GetProperty("properties").GetProperty("arch_linter_net")
                    .GetProperty("kind").GetString() == "imported_external_diagnostic"),
            Is.True);
    }

    [Test]
    public void ExternalEvidence_SameIdAsNativeContract_DoesNotCollideInMergedSarifOutput()
    {
        // Nothing in policy validation forbids external_evidence.id from matching an unrelated
        // native contract's own id (only strict_external/audit_external conflicts are forbidden).
        // ArchitectureSarifFormatter uses ContractId as ruleId for both native and imported findings,
        // so a same-named native rule descriptor must never be silently kept while an imported result
        // with the same raw id points at it — see #741 review.
        const string sharedId = "static-analysis";
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "finding")));
        string nativeSarif = JsonSerializer.Serialize(new
        {
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "arch-linter-net",
                            rules = new[] { new { id = sharedId, shortDescription = new { text = "native contract" } } },
                        },
                    },
                    results = new[]
                    {
                        new
                        {
                            ruleId = sharedId,
                            level = "error",
                            message = new { text = "native violation" },
                            locations = Array.Empty<object>(),
                        },
                    },
                },
            },
        });
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement(sharedId)),
            ForcedSarif = nativeSarif,
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        handler.Execute(ExternalEvidenceOptions(
            [Binding(sharedId, "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "sarif"));

        using JsonDocument sarif = JsonDocument.Parse(console.StdOut);
        JsonElement run = sarif.RootElement.GetProperty("runs")[0];
        JsonElement[] results = run.GetProperty("results").EnumerateArray().ToArray();
        JsonElement[] rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            // Both results are present and each still points at its own distinct rule descriptor.
            Assert.That(results, Has.Length.EqualTo(2));
            Assert.That(results.Select(result => result.GetProperty("ruleId").GetString()),
                Is.EquivalentTo([sharedId, "external-evidence:" + sharedId]));
            Assert.That(rules.Select(rule => rule.GetProperty("id").GetString()),
                Is.EquivalentTo([sharedId, "external-evidence:" + sharedId]));

            // The native rule descriptor's own description was never overwritten by the imported merge.
            JsonElement nativeRule = rules.Single(rule => rule.GetProperty("id").GetString() == sharedId);
            Assert.That(nativeRule.GetProperty("shortDescription").GetProperty("text").GetString(),
                Is.EqualTo("native contract"));

            // The imported result is still identifiable as an imported diagnostic through its own
            // namespaced rule id, not the native one.
            JsonElement importedResult = results.Single(result =>
                result.GetProperty("ruleId").GetString() == "external-evidence:" + sharedId);
            Assert.That(importedResult.GetProperty("properties").GetProperty("arch_linter_net")
                .GetProperty("kind").GetString(), Is.EqualTo("imported_external_diagnostic"));
        });
    }

    [Test]
    public void ExternalEvidence_RequiredArtifactWithZeroFindings_PassesWithNoFindings()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/zero.sarif", Sarif());
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/zero.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "json"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
        using JsonDocument json = JsonDocument.Parse(console.StdOut);
        // No selected findings for a valid zero-result run: the additive imported_diagnostics key
        // is only added when there is at least one finding (see AddImportedDiagnosticsToJson).
        Assert.That(json.RootElement.TryGetProperty("imported_diagnostics", out _), Is.False);
    }

    [Test]
    public void ExternalEvidence_TwoIndependentRequiredEvidences_BothOrdersProduceEquivalentResult()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/first.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "first")));
        repo.AddSarif("evidence/second.sarif", Sarif(Result("SEC100", "error", "src/App/Two.cs", "second")));
        ArchitectureExternalEvidenceRequirement[] requirements =
        [
            Requirement("external.first"),
            Requirement("external.second"),
        ];
        SarifEvidenceAssessmentContext context = new("repo", "revision", "scope");

        int forwardExitCode = RunWithBindings(
            repo, requirements, context,
            [Binding("external.first", "evidence/first.sarif"), Binding("external.second", "evidence/second.sarif")],
            out string forwardOutput);
        int reverseExitCode = RunWithBindings(
            repo, requirements, context,
            [Binding("external.second", "evidence/second.sarif"), Binding("external.first", "evidence/first.sarif")],
            out string reverseOutput);

        Assert.Multiple(() =>
        {
            Assert.That(forwardExitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(reverseExitCode, Is.EqualTo(CliExitCodes.Success));
            using JsonDocument forwardJson = JsonDocument.Parse(forwardOutput);
            using JsonDocument reverseJson = JsonDocument.Parse(reverseOutput);
            Assert.That(forwardJson.RootElement.GetProperty("imported_diagnostics").GetArrayLength(), Is.EqualTo(2));
            Assert.That(reverseJson.RootElement.GetProperty("imported_diagnostics").GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public void ExternalEvidence_OptionalEvidenceAbsent_Passes()
    {
        using TempRepository repo = new();
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(
                repo.Root, Requirement("external.optional", required: false)),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions([], assessmentContext: null, format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
    }

    [Test]
    public void ExternalEvidence_RequiredArtifactAbsent_IsUnassessable()
    {
        using TempRepository repo = new();
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions([], assessmentContext: null, format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.StdOut, Does.Contain("unassessable"));
    }

    [Test]
    public void ExternalEvidence_WrongRevision_IsUnassessable()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding"), revision: "old-revision"));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(
                repo.Root, Requirement("external.scan", requireRevision: true)),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "current-revision", "scope"),
            format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [Test]
    public void ExternalEvidence_UnknownBindingId_IsExecutionErrorNotUnassessable()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.unknown", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.StdErr, Does.Contain("does not match a declared"));
    }

    [Test]
    public void ExternalEvidence_UnknownBindingId_IsRejectedEvenWhenPreflightBlocked()
    {
        // ExternalEvidenceRequirements is populated on a PreflightBlocked outcome too (see
        // ArchitectureAnalysisSnapshot.BuildBlockedOutcome), so a mistyped --external-evidence id
        // must still be rejected as invalid invocation rather than silently dropped just because the
        // SARIF read/attach step itself is skipped for a blocked run — see #741 review.
        using TempRepository repo = new();
        ValidationOutcome blockedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan"))
            with
        {
            PreflightBlocked = true,
        };
        FakeCliRuntime runtime = new() { ForcedOutcome = blockedOutcome };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.unknown", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.StdErr, Does.Contain("does not match a declared"));
    }

    [Test]
    public void ExternalEvidence_ReportDestinationCollidesWithArtifactPath_IsRejected()
    {
        // A --report file sink must never be allowed to overwrite the SARIF this invocation just
        // trust-read as external evidence — see #741 review.
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));
        string evidenceFullPath = Path.Combine(repo.Root, "evidence", "current.sarif");

        ValidateCommandOptions options = ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "human") with
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, evidenceFullPath)],
        };

        int exitCode = handler.Execute(options);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.StdErr, Does.Contain("matches an --external-evidence artifact path"));
    }

    [Test]
    public void ExternalEvidence_ProfileDestinationCollidesWithArtifactPath_IsRejected()
    {
        // A --profile destination must never be allowed to overwrite the SARIF this invocation just
        // trust-read as external evidence — see #741 review.
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, Requirement("external.scan")),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));
        string evidenceFullPath = Path.Combine(repo.Root, "evidence", "current.sarif");

        ValidateCommandOptions options = ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "human") with
        {
            ProfileDestination = evidenceFullPath,
        };

        int exitCode = handler.Execute(options);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.StdErr, Does.Contain("matches an --external-evidence artifact path"));
    }

    [Test]
    public void ExternalEvidence_BlockingStrictImportedFinding_FailsGate()
    {
        using TempRepository repo = new();
        repo.AddSarif("evidence/current.sarif", Sarif(Result("SEC100", "error", "src/App/One.cs", "finding")));
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = PassingOutcomeWithRequirements(
                repo.Root,
                Requirement("external.scan", severity: new Dictionary<string, string> { ["error"] = "strict" })),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(ExternalEvidenceOptions(
            [Binding("external.scan", "evidence/current.sarif")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"),
            format: "human"));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
    }

    private int RunWithBindings(
        TempRepository repo,
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements,
        SarifEvidenceAssessmentContext context,
        IReadOnlyList<SarifEvidenceArtifactReference> bindings,
        out string output)
    {
        FakeCliRuntime runtime = new() { ForcedOutcome = PassingOutcomeWithRequirements(repo.Root, requirements.ToArray()) };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));
        int exitCode = handler.Execute(ExternalEvidenceOptions(bindings, context, format: "json"));
        output = console.StdOut;
        return exitCode;
    }

    private static ValidateCommandOptions ExternalEvidenceOptions(
        IReadOnlyList<SarifEvidenceArtifactReference> artifacts,
        SarifEvidenceAssessmentContext? assessmentContext,
        string format) =>
        new("policy.yml", "strict", format, [], null, false, null, false, false)
        {
            ExternalEvidenceArtifacts = artifacts,
            ExternalEvidenceAssessmentContext = assessmentContext,
        };

    private static ValidationOutcome PassingOutcomeWithRequirements(
        string repositoryRoot, params ArchitectureExternalEvidenceRequirement[] requirements) => new(
        Passed: true,
        Violations: Array.Empty<ArchitectureViolation>(),
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: "off",
        PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: "off",
        CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = repositoryRoot,
            ExternalEvidenceRequirements = requirements,
        };

    private static SarifEvidenceArtifactReference Binding(string id, string path) => new(path, id);

    private static ArchitectureExternalEvidenceRequirement Requirement(
        string id,
        bool required = true,
        bool requireRevision = false,
        Dictionary<string, string>? severity = null) => new()
        {
            Id = id,
            Format = "sarif",
            Required = required,
            Tool = "Synthetic.Scanner",
            ToolVersion = "1.0",
            Run = "assessment-42",
            RequireRevision = requireRevision,
            DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
            {
                Severity = severity ?? new Dictionary<string, string> { ["error"] = "audit" },
            },
        };

    private static string Result(string ruleId, string level, string path, string message) =>
        "{\"ruleId\":\"" + ruleId + "\",\"message\":{\"text\":\"" + message + "\"},\"level\":\"" + level
        + "\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\""
        + path + "\"},\"region\":{\"startLine\":7,\"startColumn\":3}}}]}";

    private static string Sarif(string? result = null, string revision = "revision") =>
        "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"Synthetic.Scanner\",\"version\":\"1.0\","
        + "\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[\"security\"]}}]}},"
        + "\"automationDetails\":{\"id\":\"assessment-42\"},\"invocations\":[{\"executionSuccessful\":true}],"
        + "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"" + revision + "\"}],"
        + "\"results\":[" + (result ?? string.Empty) + "]}]}";

    private sealed class TempRepository : IDisposable
    {
        internal TempRepository()
        {
            Root = Path.Combine(Path.GetTempPath(), $"arch-linter-cli-evidence-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        internal string Root { get; }

        internal void AddSarif(string relativePath, string content)
        {
            string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
