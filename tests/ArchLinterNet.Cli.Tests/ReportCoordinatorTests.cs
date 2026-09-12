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
internal sealed class ReportCoordinatorTests : ReportCoordinatorTestBase
{
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
        Assert.That(console.OutputText, Is.Empty);
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
    public void LegacyCombinedHuman_WritesEachModeSequentiallyWithoutHeaders()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, Array.Empty<ReportSink>());

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("Architecture validation passed."));
        Assert.That(console.OutputText, Does.Not.Contain("=== Mode:"));
    }

    [Test]
    public void RouteCombinedOutcomes_ReportModeWithHumanStdout_WritesCombinedHuman()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        var sinks = new[] { new ReportSink("human", ReportDestinationType.Stdout, null) };
        RouteResult result = coordinator.RouteCombinedOutcomes("human", outcomesByMode, sinks);

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
        Assert.That(result.FailedPaths, Is.EquivalentTo(FailedReportPaths));
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
        Assert.That(result.FailedPaths, Is.EquivalentTo(InvalidReportPaths));
    }


    [Test]
    public void ReportMode_StdoutSink_WritesFormatToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("json", ReportDestinationType.Stdout, null) };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
    }

    [Test]
    public void ReportMode_StderrAndFileSinks_RouteToRespectiveDestinations()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("human", ReportDestinationType.Stderr, null),
            new ReportSink("json", ReportDestinationType.File, "results.json"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Is.Empty);
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
        Assert.That(fileSystem.TempPaths, Does.Contain("results.json"));
    }

    [Test]
    public void Phase2Failure_TracksCommittedAndUncommitted()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "first.json"),
            new ReportSink("sarif", ReportDestinationType.File, "second.sarif"),
        };
        fileSystem.MakeUnwritable("second.sarif", phase: StubFileSystem.FailPhase.Rename);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
        Assert.That(result.CommittedPaths, Does.Contain("first.json"));
        Assert.That(result.FailedPaths, Does.Contain("second.sarif"));
        Assert.That(result.StagedPaths, Is.EquivalentTo(StagedReportPaths));
    }

    [Test]
    public void SarifFileSink_ValidatesJsonBeforeWrite()
    {
        var runtime = new InvalidJsonRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[] { new ReportSink("sarif", ReportDestinationType.File, "results.sarif") };

        var result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("results.sarif"));
    }

    [Test]
    public void ReportMode_AllFileSinksFail_ReturnsOutputFailedWithStagedPaths()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "a.json"),
            new ReportSink("sarif", ReportDestinationType.File, "b.sarif"),
        };
        fileSystem.MakeUnwritable("a.json", phase: StubFileSystem.FailPhase.Write);
        fileSystem.MakeUnwritable("b.sarif", phase: StubFileSystem.FailPhase.Write);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.StagedPaths, Is.Empty);
        Assert.That(result.CommittedPaths, Is.Empty);
    }

    [Test]
    public void ReportMode_SingleModeAllSinkTypes_CompletesSuccessfully()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.Stdout, null),
            new ReportSink("human", ReportDestinationType.Stderr, null),
            new ReportSink("sarif", ReportDestinationType.File, "report.sarif"),
        };
        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(console.OutputText, Does.Contain("kind"));
        Assert.That(console.ErrorText, Does.Contain("Architecture validation passed."));
        Assert.That(fileSystem.TempPaths, Does.Contain("report.sarif"));
    }

    [Test]
    public void PostWriteTempFileMissing_FailsBeforeAnyRename()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "vanished.sarif"),
        };
        fileSystem.MakeUnwritable("vanished.sarif", phase: StubFileSystem.FailPhase.PostWriteMissing);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        // The temp file for vanished.sarif is reported missing during staging (phase 1), so phase 2
        // never runs for either sink — good.json must not have been committed despite writing fine.
        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("vanished.sarif"));
        Assert.That(result.CommittedPaths, Is.Empty);
        Assert.That(fileSystem.TargetPaths, Is.Empty);
    }

    [Test]
    public void PostWriteTempFileCorrupted_FailsBeforeAnyRename()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("sarif", ReportDestinationType.File, "corrupted.sarif"),
        };
        fileSystem.MakeUnwritable("corrupted.sarif", phase: StubFileSystem.FailPhase.PostWriteCorrupt);

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("corrupted.sarif"));
        Assert.That(result.CommittedPaths, Is.Empty);
        Assert.That(fileSystem.TargetPaths, Is.Empty);
    }

    [Test]
    public void RouteErrorToAllSinks_WritesToFileStdoutAndStderr()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "error.json"),
            new ReportSink("human", ReportDestinationType.Stderr, null),
        };
        var contentByFormat = new Dictionary<string, string>
        {
            ["json"] = "{\"kind\":\"architecture_policy_error\"}",
            ["human"] = "Architecture validation error: bad policy",
        };

        RouteResult result = coordinator.RouteErrorToAllSinks(sinks, contentByFormat);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        Assert.That(fileSystem.TargetPaths, Does.Contain("error.json"));
        Assert.That(console.ErrorText, Does.Contain("bad policy"));
        Assert.That(console.OutputText, Is.Empty);
    }

    [Test]
    public void StreamWriteFailure_IsCaughtLikeAFileFailure_AndDoesNotAbortStaging()
    {
        // A broken stdout/stderr write (closed handle, broken pipe) must not propagate uncaught —
        // that would skip phase 2 entirely and leave whatever an earlier sink in the same batch
        // already staged as an orphaned .tmp file.
        var runtime = new CountingRuntime();
        var console = new ThrowingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "good.json"),
            new ReportSink("human", ReportDestinationType.Stderr, null),
        };

        RouteResult result = coordinator.RouteSingleOutcome("human", "strict", PassedOutcome, sinks);

        Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.OutputFailed));
        Assert.That(result.FailedPaths, Does.Contain("<stderr>"));
        Assert.That(result.ErrorDetails, Does.Contain("stream closed"));
        // The json file sink was staged before the stderr write failed; phase 1 failing must
        // discard that staged temp rather than commit it, and must not throw.
        Assert.That(result.CommittedPaths, Is.Empty);
    }

}
