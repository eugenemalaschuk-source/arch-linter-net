using ArchLinterNet.Cli.Commands.Validate;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Split out of ReportCoordinatorTests.cs (which grew past the file-size lint threshold) — issue
// #375's multi-sink commit cancellation coverage. Shares that file's private StubFileSystem/
// CountingRuntime/CapturingConsole/PassedOutcome fixtures via the partial class.
public sealed partial class ReportCoordinatorTests
{
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
            Assert.That(result.CommittedPaths, Is.EquivalentTo(new[] { "one.json" }));
            Assert.That(fileSystem.TargetPaths, Is.EquivalentTo(new[] { "one.json" }));
            Assert.That(fileSystem.FileExists("two.sarif.tmp"), Is.False);
        });
    }

    // Issue #375 PR #416 review: the legacy no-report path writes the normal document directly to
    // stdout before DistributeToSinks (which only guards file-sink staging/commit) ever runs — a
    // cancellation already observed at RouteOutcomes entry must stop that write, not just prevent
    // a file sink from being staged. Uses --format json (not human) so a single WriteLine call, not
    // the legacy-combined-human per-mode loop, is what's being guarded here.
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
}
