using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ChangeCommandHandlerTests
{
    [Test]
    public void CreateSnapshot_OrchestratesAnalysisAndWritesCoreProjectedArtifact()
    {
        var runtime = new SnapshotRuntime(Outcome("/repo", "/repo/src/Acme/Acme.csproj"));
        var console = new CapturingConsole();
        var fileSystem = new CapturingFileSystem();
        var handler = new ChangeCommandHandler(runtime, console, fileSystem);

        int exitCode = handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            "architecture/dependencies.arch.yml", "strict", "ci", null, "snapshot.json", false));

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeReports.DeserializeSnapshot(fileSystem.WrittenContents!);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidateCallCount, Is.EqualTo(1));
            Assert.That(runtime.GraphLevels, Is.EqualTo(new[] { ArchitectureGraphLevel.Namespace, ArchitectureGraphLevel.Assembly }));
            Assert.That(snapshot.ConditionSetName, Is.EqualTo("ci"));
            Assert.That(snapshot.Entries.Single().Identity, Is.EqualTo("src/Acme/Acme.csproj"));
            Assert.That(fileSystem.WrittenPath, Is.EqualTo("snapshot.json"));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void CreateSnapshot_ForwardsBuildStateToEveryContributor()
    {
        ArchitectureRunnerPreparation preparedRunner = PreparedRunner();
        var runtime = new SnapshotRuntime(Outcome("/repo", "/repo/src/Acme/Acme.csproj") with
        {
            PreparedPostBuildRunner = preparedRunner,
        });
        var handler = new ChangeCommandHandler(runtime, new CapturingConsole(), new CapturingFileSystem());

        int exitCode = handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            "architecture/dependencies.arch.yml", "strict", "ci", "baseline.yml", "snapshot.json", false,
            EnsureBuilt: true, NoRestore: true, Configuration: "Release", TargetFramework: "net10.0",
            Platform: "AnyCPU", RuntimeIdentifier: "win-x64"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidationRequest, Is.Not.Null);
            Assert.That(runtime.ValidationRequest!.PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            Assert.That(runtime.ValidationRequest.NoRestore, Is.True);
            Assert.That(runtime.ValidationRequest.RequestedConfiguration, Is.EqualTo("Release"));
            Assert.That(runtime.ValidationRequest.RequestedTargetFramework, Is.EqualTo("net10.0"));
            Assert.That(runtime.ValidationRequest.RequestedPlatform, Is.EqualTo("AnyCPU"));
            Assert.That(runtime.ValidationRequest.RequestedRuntimeIdentifier, Is.EqualTo("win-x64"));
            Assert.That(runtime.GraphRequests, Has.Count.EqualTo(2));
            Assert.That(runtime.GraphRequests, Has.All.Matches<ArchitectureGraphRequest>(request =>
                request.PreparationMode == BuildPreparationMode.Ordinary
                && request.UsePreparedPostBuildState
                && request.NoRestore
                && request.RequestedConfiguration == "Release"
                && request.RequestedTargetFramework == "net10.0"
                && request.RequestedPlatform == "AnyCPU"
                && request.RequestedRuntimeIdentifier == "win-x64"
                && ReferenceEquals(request.PreparedPostBuildRunner, preparedRunner)));
            Assert.That(runtime.BaselineDiffRequest, Is.Not.Null);
            Assert.That(runtime.BaselineDiffRequest!.PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
            Assert.That(runtime.BaselineDiffRequest.UsePreparedPostBuildState, Is.True);
            Assert.That(runtime.BaselineDiffRequest.NoRestore, Is.True);
            Assert.That(runtime.BaselineDiffRequest.RequestedConfiguration, Is.EqualTo("Release"));
            Assert.That(runtime.BaselineDiffRequest.RequestedTargetFramework, Is.EqualTo("net10.0"));
            Assert.That(runtime.BaselineDiffRequest.RequestedPlatform, Is.EqualTo("AnyCPU"));
            Assert.That(runtime.BaselineDiffRequest.RequestedRuntimeIdentifier, Is.EqualTo("win-x64"));
            Assert.That(runtime.BaselineDiffRequest.PreparedPostBuildRunner, Is.SameAs(preparedRunner));
        });
    }

    [Test]
    public void CreateSnapshot_EnsureBuiltWithoutPreparedSelection_FailsWithoutWritingSnapshot()
    {
        var runtime = new SnapshotRuntime(Outcome("/repo", "/repo/src/Acme/Acme.csproj"));
        var console = new CapturingConsole();
        var fileSystem = new CapturingFileSystem();
        var handler = new ChangeCommandHandler(runtime, console, fileSystem);

        int exitCode = handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            "architecture/dependencies.arch.yml", "strict", null, null, "snapshot.json", false,
            EnsureBuilt: true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.GraphRequests, Is.Empty);
            Assert.That(fileSystem.WrittenContents, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("validation did not produce complete analysis facts"));
        });
    }

    [Test]
    public void CreateSnapshot_BlockedBaselineDebt_FailsWithoutWritingPartialSnapshot()
    {
        var runtime = new SnapshotRuntime(Outcome("/repo", "/repo/src/Acme/Acme.csproj"))
        {
            DiffOutcome = new BaselineDiffOutcome(
                Succeeded: false,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: Array.Empty<ArchitectureViolation>())
            {
                PreflightDiagnostics = new[]
                {
                    new BuildStatePreflightDiagnostic(
                        "baseline", "Acme.csproj", BuildStatePreflightState.MissingArtifact,
                        new BuildStatePreflightEvidence("Acme.csproj", "Acme")),
                },
            },
        };
        var console = new CapturingConsole();
        var fileSystem = new CapturingFileSystem();
        var handler = new ChangeCommandHandler(runtime, console, fileSystem);

        int exitCode = handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            "architecture/dependencies.arch.yml", "strict", null, "baseline.yml", "snapshot.json", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.WrittenContents, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("baseline debt did not produce complete analysis facts"));
            Assert.That(console.ErrorText, Does.Contain("preflight diagnostic"));
        });
    }

    [Test]
    public void CreateSnapshot_BlockedValidation_FailsBeforeGraphProjectionOrWriting()
    {
        ValidationOutcome blocked = Outcome("/repo", "/repo/src/Acme/Acme.csproj") with
        {
            PreflightBlocked = true,
            PreflightDiagnostics = new[]
            {
                new BuildStatePreflightDiagnostic(
                    "validation", "Acme.csproj", BuildStatePreflightState.MissingArtifact,
                    new BuildStatePreflightEvidence("Acme.csproj", "Acme")),
            },
        };
        var runtime = new SnapshotRuntime(blocked);
        var console = new CapturingConsole();
        var fileSystem = new CapturingFileSystem();
        var handler = new ChangeCommandHandler(runtime, console, fileSystem);

        int exitCode = handler.CreateSnapshot(new ChangeSnapshotCommandOptions(
            "architecture/dependencies.arch.yml", "strict", null, null, "snapshot.json", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.GraphRequests, Is.Empty);
            Assert.That(fileSystem.WrittenContents, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("validation did not produce complete analysis facts"));
        });
    }

    [Test]
    public void OutputCollisionGuards_RejectEveryChangeCommandInput()
    {
        ChangeSnapshotCommandOptions snapshotWithPolicyCollision = new(
            "policy.yml", "strict", null, "baseline.yml", "policy.yml", false);
        ChangeSnapshotCommandOptions snapshotWithBaselineCollision = new(
            "policy.yml", "strict", null, "baseline.yml", "baseline.yml", false);
        ChangeReportCommandOptions reportWithBaseCollision = new(
            "base.json", "current.json", "json", "base.json", false, "run");
        ChangeReportCommandOptions reportWithCurrentCollision = new(
            "base.json", "current.json", "json", "current.json", false, "run");

        Assert.Multiple(() =>
        {
            Assert.That(ChangeCommandHandler.FindSnapshotOutputCollision(snapshotWithPolicyCollision), Does.Contain("--policy"));
            Assert.That(ChangeCommandHandler.FindSnapshotOutputCollision(snapshotWithBaselineCollision), Does.Contain("--baseline"));
            Assert.That(ChangeCommandHandler.FindReportOutputCollision(reportWithBaseCollision), Does.Contain("--base"));
            Assert.That(ChangeCommandHandler.FindReportOutputCollision(reportWithCurrentCollision), Does.Contain("--current"));
        });
    }

    [Test]
    public void SnapshotConsumedInputCollisionGuard_RejectsEveryPostAnalysisInput()
    {
        ValidationOutcome outcome = Outcome("/repo", "/repo/src/Acme/Acme.csproj") with
        {
            PolicyImportPaths = new[] { "/repo/architecture/imported.yml" },
            ResolvedAssemblyPaths = new[] { "/repo/bin/Acme.dll" },
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                ChangeCommandHandler.FindSnapshotConsumedInputCollision("/repo/architecture/imported.yml", outcome),
                Does.Contain("imported policy file"));
            Assert.That(
                ChangeCommandHandler.FindSnapshotConsumedInputCollision("/repo/bin/Acme.dll", outcome),
                Does.Contain("build artifact"));
            Assert.That(
                ChangeCommandHandler.FindSnapshotConsumedInputCollision(
                    BuildReceiptStore.ReceiptPathFor("/repo/bin/Acme.dll"), outcome),
                Does.Contain("build receipt"));
            Assert.That(
                ChangeCommandHandler.FindSnapshotConsumedInputCollision("/repo/src/Acme/Acme.csproj", outcome),
                Does.Contain("project file"));
        });
    }

    private static ValidationOutcome Outcome(string repositoryRoot, string projectPath) => new(
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
        DiscoveredProjectPaths = new[] { projectPath },
    };

    private static ArchitectureRunnerPreparation PreparedRunner() => new(
        "/repo",
        null,
        ProjectDiscoveryResult.Empty,
        ResolveAssemblyOutputs: true,
        SelectedAssemblyArtifactPaths: ["/repo/bin/Release/net10.0/win-x64/Acme.dll"],
        CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/repo/bin/Release/net10.0/win-x64/Acme.dll"] = "digest",
        },
        MissingAssemblyNames: Array.Empty<string>(),
        IsMetadataReferenceClosureComplete: true);

    private sealed class SnapshotRuntime(ValidationOutcome outcome) : ICliRuntime
    {
        public int ValidateCallCount { get; private set; }

        public List<ArchitectureGraphLevel> GraphLevels { get; } = new();

        public List<ArchitectureGraphRequest> GraphRequests { get; } = new();

        public ValidationRequest? ValidationRequest { get; private set; }

        public BaselineDiffRequest? BaselineDiffRequest { get; private set; }

        public BaselineDiffOutcome? DiffOutcome { get; init; }

        public string Version => "1.2.3";

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            ValidateCallCount++;
            ValidationRequest = request;
            return outcome;
        }

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode, bool passed,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
            IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
            IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            "preflight diagnostic";

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) =>
            throw new NotSupportedException();

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) =>
            throw new NotSupportedException();

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) =>
            throw new NotSupportedException();

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) =>
            throw new NotSupportedException();

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) =>
            throw new NotSupportedException();

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
        {
            BaselineDiffRequest = request;
            return DiffOutcome ?? new BaselineDiffOutcome(
                Succeeded: true,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: Array.Empty<ArchitectureViolation>());
        }
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();
        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();
        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();
        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();
        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
        {
            GraphLevels.Add(request.Level);
            GraphRequests.Add(request);
            return new ArchitectureGraphOutcome(new ArchitectureDependencyGraph(
                Array.Empty<ArchitectureGraphNode>(),
                Array.Empty<ArchitectureGraphEdge>()));
        }

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }

    private sealed class CapturingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public TextWriter Out => new StringWriter(_output);

        public TextWriter Error => new StringWriter(_error);

        public string ErrorText => _error.ToString();
    }

    private sealed class CapturingFileSystem : IFileSystem
    {
        public string? WrittenPath { get; private set; }

        public string? WrittenContents { get; private set; }

        public bool FileExists(string path) => false;

        public string ReadAllText(string path) => throw new NotSupportedException();

        public void WriteAllText(string path, string contents)
        {
            WrittenPath = path;
            WrittenContents = contents;
        }

        public string WriteAllTextToTemp(string targetPath, string contents) => throw new NotSupportedException();

        public void RenameTempToTarget(string tempPath, string targetPath) => throw new NotSupportedException();

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => throw new NotSupportedException();

        public void DeleteFile(string path) => throw new NotSupportedException();

        public bool TryCreateNewFile(string path) => throw new NotSupportedException();

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public void DeleteDirectoryIfEmpty(string path) => throw new NotSupportedException();

        public bool CanWriteToDirectory(string path) => throw new NotSupportedException();
    }
}
