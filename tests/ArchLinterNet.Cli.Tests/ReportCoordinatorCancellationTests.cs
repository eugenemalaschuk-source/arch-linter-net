using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Split out of ReportCoordinatorTests.cs (which grew past the file-size lint threshold) — issue
// #375's multi-sink commit cancellation coverage. Shares that file's private StubFileSystem/
// CountingRuntime/CapturingConsole/PassedOutcome fixtures via the partial class.
public sealed partial class ReportCoordinatorTests
{
    private static readonly string[] _oneJsonPath = { "one.json" };
    private static readonly string[] _oneJsonAndTwoSarifPaths = { "one.json", "two.sarif" };
    private static readonly string[] _pkgACycle = { "pkg-a -> pkg-b -> pkg-a" };

    // Issue #375: cancellation observed before staging even begins must report zero committed
    // files, Cancelled=true, and never call WriteAllTextToTemp for any sink.
    [Test]
    public void RouteSingleOutcome_CancelledBeforeStaging_ReportsCancelledWithNothingCommitted()
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
        using CancellationTokenSource cts = new();
        cts.Cancel();

        RouteResult result = coordinator.RouteSingleOutcome(
            "human", "strict", PassedOutcome, sinks, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.CommittedPaths, Is.Empty);
            Assert.That(result.UncommittedPaths, Is.EquivalentTo(_oneJsonAndTwoSarifPaths));
            Assert.That(fileSystem.TempPaths, Is.Empty);
        });
    }

    // Issue #375: cancellation observed mid-commit (after one sink already renamed into place)
    // must keep that sink committed — no rollback — while the still-pending sink's staged temp is
    // removed instead of renamed, and the result reports the existing typed partial-output
    // evidence (PartialOutput, since one file did commit) plus Cancelled=true.
    [Test]
    public void RouteSingleOutcome_CancelledMidCommit_KeepsAlreadyRenamedFileAndCleansUpRemainingTemp()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);
        using CancellationTokenSource cts = new();
        fileSystem.OnRenamed = renamedPath =>
        {
            if (renamedPath == "one.json")
            {
                cts.Cancel();
            }
        };

        var sinks = new[]
        {
            new ReportSink("json", ReportDestinationType.File, "one.json"),
            new ReportSink("sarif", ReportDestinationType.File, "two.sarif"),
        };

        RouteResult result = coordinator.RouteSingleOutcome(
            "human", "strict", PassedOutcome, sinks, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
            Assert.That(result.CommittedPaths, Is.EquivalentTo(_oneJsonPath));
            Assert.That(fileSystem.TargetPaths, Is.EquivalentTo(_oneJsonPath));
            Assert.That(fileSystem.FileExists("two.sarif.tmp"), Is.False);
        });
    }

    // Issue #375 PR #416 review: the legacy no-report path writes the normal document directly to
    // stdout before DistributeToSinks (which only guards file-sink staging/commit) ever runs — a
    // cancellation already observed at RouteOutcomes entry must stop that write, not just prevent
    // a file sink from being staged. Uses --format json (not human) so a single WriteLine call, not
    // the legacy-combined-human per-mode loop, is what's being guarded here.
    // Issue #375 PR #416 review: proves the coordinator's own error-routing entrypoint
    // (RouteErrorToAllSinks — used by ValidateCommandHandler.WriteErrorContent for pre-outcome
    // policy/execution errors) honors a cancelled token on its own DistributeToSinks early-return,
    // independent of RouteOutcomes' pre-render guard (RouteErrorToAllSinks never goes through
    // RouteOutcomes at all).
    [Test]
    public void RouteErrorToAllSinks_CancelledToken_ReportsCancelledWithoutStaging()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        RouteResult result = coordinator.RouteErrorToAllSinks(
            new[] { new ReportSink("json", ReportDestinationType.File, "error.json") },
            new Dictionary<string, string> { ["json"] = "{}" },
            cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.CommittedPaths, Is.Empty);
            Assert.That(fileSystem.TempPaths, Is.Empty);
        });
    }

    [Test]
    public void RouteSingleOutcome_CancelledBeforeRendering_NoAdditionalSinks_NeverWritesToStdout()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        RouteResult result = coordinator.RouteSingleOutcome(
            "json", "strict", PassedOutcome, Array.Empty<ReportSink>(), cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Cancelled, Is.True);
            Assert.That(console.OutputText, Is.Empty);
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void RouteSingleOutcome_StreamOnlyCancellationAfterFinalWrite_RemainsSuccessful()
    {
        var runtime = new CountingRuntime();
        using CancellationTokenSource cts = new();
        var console = new CapturingConsole { OnOutputWriteLine = cts.Cancel };
        var coordinator = new ReportCoordinator(runtime, console, new StubFileSystem());

        RouteResult result = coordinator.RouteSingleOutcome(
            "human", "strict", PassedOutcome,
            [new ReportSink("json", ReportDestinationType.Stdout)], cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText, Does.Contain("kind"));
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
        });
    }

    [Test]
    public void RouteCombinedOutcomes_LegacyHumanCancellationAfterFirstWrite_ReportsDeliveredStdout()
    {
        var runtime = new CountingRuntime();
        using CancellationTokenSource cts = new();
        var console = new CapturingConsole { OnOutputWriteLine = cts.Cancel };
        var coordinator = new ReportCoordinator(runtime, console, new StubFileSystem());

        RouteResult result = coordinator.RouteCombinedOutcomes(
            "human", [("strict", PassedOutcome), ("audit", PassedOutcome)], Array.Empty<ReportSink>(), cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.PartialOutput));
            Assert.That(result.DeliveredStreamPaths, Is.EquivalentTo(new[] { "<stdout>" }));
            Assert.That(result.UncommittedPaths, Is.Empty);
        });
    }

    // PR #416 review: FormatSingle/CombinedHuman/Json/Sarif previously accepted no token at all,
    // so a large findings set could fully serialize after Ctrl+C before the next check. These
    // prove cancellation observed mid-render — at the boundary between two of a human report's
    // sections, or between two modes of a combined json/sarif document — stops before the
    // remaining section/mode renders, instead of only being checked before the whole render started.
    private static ValidationOutcome ViolationsAndCyclesOutcome => new(
        false,
        new[] { new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()) },
        _pkgACycle, Array.Empty<ArchitectureViolation>(), "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
        Array.Empty<PolicyConsistencyDiagnostic>(), "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    [Test]
    public void RouteSingleOutcome_CancelledDuringHumanRendering_StopsBeforeLaterSectionRenders()
    {
        using CancellationTokenSource cts = new();
        var runtime = new CountingRuntime { OnFormatViolationsForHumans = cts.Cancel };
        var console = new CapturingConsole();
        var coordinator = new ReportCoordinator(runtime, console, new StubFileSystem());

        Assert.Throws<OperationCanceledException>(() =>
            coordinator.RouteSingleOutcome("human", "strict", ViolationsAndCyclesOutcome, Array.Empty<ReportSink>(), cts.Token));

        Assert.That(runtime.HumanCallCount, Is.EqualTo(1), "FormatCyclesForHumans must not run once cancellation was observed after violations rendered");
        Assert.That(console.OutputText, Is.Empty, "no partial document may reach stdout");
    }

    [Test]
    public void RouteCombinedOutcomes_CancelledDuringJsonRendering_StopsBeforeSecondModeRenders()
    {
        using CancellationTokenSource cts = new();
        var runtime = new CountingRuntime { OnFormatResultForCiArtifacts = cts.Cancel };
        var console = new CapturingConsole();
        var coordinator = new ReportCoordinator(runtime, console, new StubFileSystem());

        var outcomesByMode = new[] { ("strict", PassedOutcome), ("audit", FailedOutcome) };
        var sinks = new[] { new ReportSink("json", ReportDestinationType.File, "results.json") };

        Assert.Throws<OperationCanceledException>(() =>
            coordinator.RouteCombinedOutcomes("human", outcomesByMode, sinks, cts.Token));

        Assert.That(runtime.JsonCallCount, Is.EqualTo(1), "the audit mode's JSON must not be rendered once cancellation was observed after strict rendered");
    }
}
