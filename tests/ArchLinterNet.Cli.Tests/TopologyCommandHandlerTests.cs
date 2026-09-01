using System.CommandLine;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class TopologyCommandHandlerTests
{
    [Test]
    public void TopologyModule_ComposesCaptureDiffAndVerifySubcommands()
    {
        Command command = new TopologyCommandModule().CreateCommand(new FakeRuntime(), new FakeConsole(), new FakeFileSystem());

        Assert.That(command.Subcommands.Select(subcommand => subcommand.Name),
            Is.EqualTo(new[] { "capture", "diff", "verify" }));
    }

    [Test]
    public void CaptureJson_IsByteStableAndOrdersFacts()
    {
        FakeConsole console = new();
        FakeFileSystem files = new();
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome(
                "assembly",
                [
                    new("b", "assembly", "B", "PB", "AB"),
                    new("a", "assembly", "A", "PA", "AA"),
                ],
                [new("b", "a", "B -> A"), new("a", "b", "A -> B")],
                "repo",
                ["z.yml", "a.yml"], ["z.dll", "a.dll"], ["z.csproj", "a.csproj"],
                [],
                false),
        };
        TopologyCommandHandler handler = new(runtime, console, files);
        TopologyCaptureCommandOptions options = new("policy.yml", "assembly", "json", null, null, false);

        Assert.That(handler.Capture(options), Is.EqualTo(CliExitCodes.Success));
        string first = console.Output;
        console.Clear();
        Assert.That(handler.Capture(options), Is.EqualTo(CliExitCodes.Success));

        Assert.That(console.Output, Is.EqualTo(first));
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("topology-capture"));
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("subjects")[0].GetProperty("identity").GetString(), Is.EqualTo("a"));
            Assert.That(document.RootElement.GetProperty("policy_import_paths")[0].GetString(), Is.EqualTo("a.yml"));
        });
    }

    [Test]
    public void CaptureOutput_RejectsConsumedInputAfterCapture()
    {
        string policy = Path.GetFullPath("policy.yml");
        FakeConsole console = new();
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome(
                "assembly", [], [], "repo", [policy], ["a.dll"], ["a.csproj"], []),
        };

        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Capture(new(policy, "assembly", "json", policy, null, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("output-collision"));
            Assert.That(runtime.CaptureCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Diff_ProjectsDistinctCategoriesAndCallsValidationOnce()
    {
        FakeConsole console = new();
        ArchitectureTopologyMappingEvidence evidence = new(
            "exhaustive", "assembly", 3,
            [
                new("amb", "P", "A", "Ambiguous", "ambiguous", ["one", "two"]),
                new("unmapped", "P", "A", "Unmapped", "unmapped"),
                new("out", "P", "A", "Out", "reviewed_out_of_scope", [], "review"),
            ],
            [new("one", "two", "A -> B", false), new("one", "three", "A -> C", true)],
            ["retired"], [new("retired", "old")]);
        FakeRuntime runtime = new() { ValidationResult = Outcome(evidence, passed: false) };
        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("structural").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("relational")[0].GetProperty("witness").GetString(), Is.EqualTo("A -> B"));
            Assert.That(document.RootElement.GetProperty("unmapped").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("stale").GetProperty("nodes")[0].GetString(), Is.EqualTo("retired"));
            Assert.That(document.RootElement.GetProperty("reviewed_out_of_scope").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    public void Verify_UsesNormalJsonEnvelopeAndPreservesAuditMode()
    {
        ArchitectureTopologyMappingEvidence evidence = new(
            "partial", "assembly", 1, [], [], [], []);
        FakeRuntime runtime = new()
        {
            ValidationResult = Outcome(evidence, passed: true),
            ValidationJson = "{\"passed\":true,\"mode\":\"audit\",\"violations\":[]}",
        };
        FakeConsole console = new();
        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Verify(new("policy.yml", "audit", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(runtime.LastValidationRequest!.Mode, Is.EqualTo("audit"));
            Assert.That(document.RootElement.GetProperty("mode").GetString(), Is.EqualTo("audit"));
            Assert.That(document.RootElement.TryGetProperty("kind", out _), Is.False);
        });
    }

    [Test]
    public void DiffWithoutTopology_IsTypedInputErrorAfterOneValidation()
    {
        FakeRuntime runtime = new() { ValidationResult = Outcome(null, passed: true) };
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(console.Output, Does.Contain("no-declared-topology"));
        });
    }

    private static ValidationOutcome Outcome(ArchitectureTopologyMappingEvidence? evidence, bool passed)
    {
        ArchitectureApplicabilityRecord[] records = evidence is null
            ? []
            : [new ArchitectureApplicabilityRecord("declared-topology", "declared_topology",
                ArchitectureApplicabilityRecordState.Evaluable)
            {
                TopologyEvidence = evidence,
            }];
        return new ValidationOutcome(passed, [], [], [], "off", [], "off", [], "off", [], [], [])
        {
            ApplicabilityRecords = records,
            RepositoryRoot = Path.GetFullPath("."),
        };
    }

    private sealed class FakeRuntime : ICliRuntime
    {
        public string Version => "test";
        public ArchitectureTopologyCaptureOutcome CaptureResult { get; init; } = new("assembly", [], [], "repo", [], [], [], []);
        public ValidationOutcome ValidationResult { get; init; } = Outcome(null, true);
        public string ValidationJson { get; init; } = "{\"passed\":true,\"mode\":\"strict\",\"violations\":[]}";
        public int CaptureCalls { get; private set; }
        public int ValidateCalls { get; private set; }
        public ValidationRequest? LastValidationRequest { get; private set; }
        public ArchitectureTopologyCaptureOutcome CaptureTopology(ArchitectureTopologyCaptureRequest request)
        {
            CaptureCalls++;
            return CaptureResult;
        }
        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            ValidateCalls++;
            LastValidationRequest = request;
            return ValidationResult;
        }
        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => Enum.TryParse(value, true, out level);
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => ValidationJson;
        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => "{}";
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => string.Empty;
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => string.Empty;
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => string.Empty;
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => string.Empty;
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => string.Empty;
        public string FormatClassificationFactsForHumans(IReadOnlyCollection<ArchitectureClassificationConflict> conflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => string.Empty;
        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => string.Empty;
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => string.Empty;
        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();
        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();
        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();
        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string Output => _output.ToString();
        public string ErrorText => _error.ToString();
        public void Clear() { _output.Clear(); _error.Clear(); }
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public bool FileExists(string path) => false;
        public string ReadAllText(string path) => string.Empty;
        public void WriteAllText(string path, string contents) { }
        public string WriteAllTextToTemp(string targetPath, string contents) => targetPath;
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
