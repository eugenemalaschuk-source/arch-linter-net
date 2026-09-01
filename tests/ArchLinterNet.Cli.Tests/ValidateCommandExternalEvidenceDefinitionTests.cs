using System.CommandLine;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate.EntryPoint;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Option-parsing coverage for --external-evidence/--evidence-repository/--evidence-revision/
// --evidence-scope, mirroring the existing --report parsing test pattern in
// ValidateCommandDefinitionTests. These tests exercise only ValidateCommandDefinition's parsing —
// see ArchitectureExternalEvidenceBinderTests (Core) for the trust/selection/applicability behavior
// bound artifacts eventually flow into, and ValidateCommandHandlerExternalEvidenceTests (Cli) for the
// full end-to-end wiring.
[TestFixture]
public sealed class ValidateCommandExternalEvidenceDefinitionTests
{
    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_ValidSingleBinding_ReachesRuntime()
    {
        (RecordingRuntime runtime, _) = Run(["--external-evidence", "id=external.scan,path=evidence/scan.sarif"]);

        Assert.That(runtime.LastRequest, Is.Not.Null);
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_ValidBindingWithProducerContext_ReachesRuntime()
    {
        (RecordingRuntime runtime, _) = Run([
            "--external-evidence",
            "id=external.scan,path=evidence/scan.sarif,repository=repo,revision=rev,scope=ci",
        ]);

        Assert.That(runtime.LastRequest, Is.Not.Null);
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_TwoBindings_BothReachRuntime()
    {
        (RecordingRuntime runtime, _) = Run([
            "--external-evidence", "id=external.first,path=evidence/first.sarif",
            "--external-evidence", "id=external.second,path=evidence/second.sarif",
        ]);

        Assert.That(runtime.LastRequest, Is.Not.Null);
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_MissingId_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run(["--external-evidence", "path=evidence/scan.sarif"]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Missing required 'id'"));
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_MissingPath_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run(["--external-evidence", "id=external.scan"]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Missing required 'path'"));
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_UnknownKey_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run([
            "--external-evidence", "id=external.scan,path=evidence/scan.sarif,bogus=value",
        ]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Invalid --external-evidence key 'bogus'"));
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_MalformedKeyValueSyntax_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run(["--external-evidence", "not-a-key-value-pair"]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Invalid --external-evidence value"));
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_DuplicateKeyWithinOneBinding_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run([
            "--external-evidence", "id=external.scan,path=one.sarif,path=two.sarif",
        ]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Duplicate key 'path'"));
    }

    [Test]
    public void CreateRootCommand_ExternalEvidenceOption_DuplicateBindingId_IsRejected()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run([
            "--external-evidence", "id=external.scan,path=first.sarif",
            "--external-evidence", "id=external.scan,path=second.sarif",
        ]);

        Assert.That(runtime.LastRequest, Is.Null);
        Assert.That(console.ErrorText, Does.Contain("Duplicate --external-evidence binding for id 'external.scan'"));
    }

    [Test]
    public void CreateRootCommand_EvidenceContextOptions_ReachRuntimeWithoutError()
    {
        (RecordingRuntime runtime, RecordingConsole console) = Run([
            "--evidence-repository", "repo",
            "--evidence-revision", "rev",
            "--evidence-scope", "ci",
        ]);

        Assert.That(runtime.LastRequest, Is.Not.Null);
        Assert.That(console.ErrorText, Is.Empty);
    }

    private static (RecordingRuntime Runtime, RecordingConsole Console) Run(string[] args)
    {
        var runtime = new RecordingRuntime();
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem(true);
        RootCommand command = new ValidateCommandModule().CreateRootCommand(runtime, console, fileSystem);

        command.Parse(args).Invoke();
        return (runtime, console);
    }

    private sealed class RecordingFileSystem(bool exists) : IFileSystem
    {
        public bool FileExists(string path) => exists;
        public string ReadAllText(string path) => "{}";
        public void WriteAllText(string path, string contents) { }
        public string WriteAllTextToTemp(string targetPath, string contents) => targetPath + ".tmp";
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => !FileExists(targetPath);
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string OutputText => _output.ToString();
        public string ErrorText => _error.ToString();
    }

    private sealed class RecordingRuntime : ICliRuntime
    {
        public string Version => "1.2.3";
        public ValidationRequest? LastRequest { get; private set; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
        {
            level = ArchitectureGraphLevel.Namespace;
            return true;
        }

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            LastRequest = request;
            return new ValidationOutcome(
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
                RepositoryRoot = Path.GetTempPath(),
            };
        }

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
            IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
            IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => "{}";

        public string FormatResultAsSarif(
            string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => "{}";

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => string.Empty;

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => string.Empty;

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => string.Empty;

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => string.Empty;

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => string.Empty;

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => string.Empty;

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;

        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) =>
            throw new NotSupportedException();

        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) =>
            throw new NotSupportedException();

        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) =>
            throw new NotSupportedException();

        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) =>
            throw new NotSupportedException();

        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) =>
            throw new NotSupportedException();

        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) =>
            throw new NotSupportedException();

        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) =>
            throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) =>
            throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) =>
            throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) =>
            throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) =>
            throw new NotSupportedException();

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => string.Empty;

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => string.Empty;

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) =>
            throw new NotSupportedException();
    }
}
