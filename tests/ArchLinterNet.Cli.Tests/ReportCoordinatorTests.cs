using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ReportCoordinatorTests
{
    private static ValidationOutcome PassedOutcome => new(
        true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
        Array.Empty<ArchitectureViolation>(), "off", Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        "off", Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    private static ValidationOutcome FailedOutcome => new(
        false,
        new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
        Array.Empty<string>(), Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
        Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    [Test]
    public void RouteSingleOutcome_HumanFormatNoAdditionalSinks_WritesHumanToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, []);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(result.FailedPaths, Is.Empty);
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
        Assert.That(console.ErrorText, Is.Empty);
    }

    [Test]
    public void RouteSingleOutcome_JsonFormatNoAdditionalSinks_WritesJsonToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("json", "strict", PassedOutcome, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RouteSingleOutcome_SarifFormatNoAdditionalSinks_WritesSarifToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        RouteResult result = coordinator.RouteSingleOutcome("sarif", "strict", PassedOutcome, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("version"));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RouteSingleOutcome_HumanWithStderrSink_WritesHumanToBothStdoutAndStderr()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("human", ReportDestinationType.Stderr, null) };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
    }

    [Test]
    public void RouteSingleOutcome_JsonSinkWithHumanStdout_WritesTempThenRenames()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.File, "output.json") };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(fileSystem.TempPaths, Does.Contain("output.json"));
        Assert.That(fileSystem.TargetPaths, Does.Contain("output.json"));
    }

    [Test]
    public void RouteSingleOutcome_DifferentFormats_FormatMethodsCalledOnceEach()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.Stderr, null),
            new ReportSink("sarif", ReportDestinationType.File, "results.sarif"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(runtime.HumanCallCount, Is.EqualTo(0));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(1));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RouteCombinedOutcomes_MultipleModes_WritesCombinedHuman()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("=== Mode: strict ==="));
        Assert.That(console.OutputText, Does.Contain("=== Mode: audit ==="));
    }

    [Test]
    public void RouteCombinedOutcomes_DifferentFormats_FormatMethodsCalledPerMode()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "results.json"),
            new ReportSink("sarif", ReportDestinationType.File, "results.sarif"),
        };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(runtime.JsonCallCount, Is.EqualTo(2));
        Assert.That(runtime.SarifCallCount, Is.EqualTo(2));
    }

    [Test]
    public void RouteSingleOutcome_UnwriteableFileSink_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new FailingFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.File, "output.json") };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("output.json"));
        Assert.That(result.ErrorDetails, Is.Not.Empty);
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
    }

    [Test]
    public void RouteSingleOutcome_OneFileSinkFailsPhase2_ReturnsPartialOutput()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "bad.sarif"),
        };
        fileSystem.MakeUnwritable("bad.sarif", phase: StubFileSystem.FailPhase.Rename);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
        Assert.That(result.FailedPaths, Does.Contain("bad.sarif"));
        Assert.That(result.FailedPaths, Does.Not.Contain("good.json"));
    }

    [Test]
    public void RouteSingleOutcome_TempWriteFailsForAll_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "one.json"),
            new ReportSink("sarif", ReportDestinationType.File, "two.sarif"),
        };
        fileSystem.MakeUnwritable("one.json", phase: StubFileSystem.FailPhase.Write);
        fileSystem.MakeUnwritable("two.sarif", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Is.EquivalentTo(new[] { "one.json", "two.sarif" }));
    }

    [Test]
    public void RouteSingleOutcome_FirstTempWriteFailsAllRenamesSkipped_ReturnsOutputFailed()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "bad.json"),
            new ReportSink("sarif", ReportDestinationType.File, "good.sarif"),
        };
        fileSystem.MakeUnwritable("bad.json", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        // Phase 1: bad.json fails, good.sarif temp written.
        // Phase 2: skipped entirely.
        // No file was renamed → no output published → OutputFailed, not PartialOutput.
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Is.EquivalentTo(new[] { "bad.json" }));
    }

    private sealed class CapturingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string OutputText => _output.ToString();
        public string ErrorText => _error.ToString();
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public enum FailPhase { Write, Rename }

        private readonly record struct FailEntry(string Path, FailPhase Phase);
        private readonly HashSet<FailEntry> _failOn = new();

        public List<string> TempPaths { get; } = new();
        public List<string> TargetPaths { get; } = new();

        public void MakeUnwritable(string path, FailPhase phase = FailPhase.Write) =>
            _failOn.Add(new FailEntry(path, phase));

        public bool FileExists(string path) => false;

        public void WriteAllText(string path, string contents) { }

        public void WriteAllTextToTemp(string path, string contents)
        {
            if (_failOn.Contains(new FailEntry(path, FailPhase.Write)))
            {
                throw new IOException($"Cannot write to {path}");
            }

            TempPaths.Add(path);
        }

        public string ResolveTempPath(string path) => path + ".tmp";

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            string original = tempPath.EndsWith(".tmp") ? tempPath[..^4] : tempPath;
            if (_failOn.Contains(new FailEntry(original, FailPhase.Rename)))
            {
                throw new IOException($"Cannot rename to {targetPath}");
            }

            TargetPaths.Add(targetPath);
        }

        public void DeleteFile(string path)
        {
        }

        public bool CanWriteToDirectory(string path) => !_failOn.Contains(new FailEntry(path, FailPhase.Write));
    }

    private sealed class FailingFileSystem : IFileSystem
    {
        public bool FileExists(string path) => false;
        public void WriteAllText(string path, string contents) { }
        public void WriteAllTextToTemp(string path, string contents) => throw new IOException("Disk full");
        public string ResolveTempPath(string path) => path + ".tmp";
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public void DeleteFile(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class CountingRuntime : ICliRuntime
    {
        public int HumanCallCount { get; private set; }
        public int JsonCallCount { get; private set; }
        public int SarifCallCount { get; private set; }

        public string Version => "1.2.3";

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

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
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            JsonCallCount++;
            return "{\"kind\":\"validation\",\"passed\":true}";
        }

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
        {
            SarifCallCount++;
            return "{\"version\":\"2.1.0\",\"runs\":[]}";
        }

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            string.Empty;

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) { HumanCallCount++; return "violations"; }
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) { HumanCallCount++; return "cycles"; }
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => "pc";
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => "unmatched";
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => "coverage";
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => "summary";
        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => "classifications";

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();
        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }
}
