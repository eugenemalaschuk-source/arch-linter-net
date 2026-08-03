using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class ReportCoordinatorTests
{
    private static readonly string[] _allReportFormats = { "human", "json", "sarif" };

    [Test]
    public void RouteSingleOutcome_CompletedRendersAndPublicationPhases_AreProfiled()
    {
        var runtime = new CountingRuntime();
        var console = new CapturingConsole();
        var fileSystem = new StubFileSystem();
        var coordinator = new ReportCoordinator(runtime, console, fileSystem);
        ValidationTiming timing = new();

        RouteResult result;
        using (timing.Measure("total"))
        {
            result = coordinator.RouteSingleOutcome(
                "human", "strict", PassedOutcome,
                [
                    new ReportSink("human", ReportDestinationType.Stdout),
                    new ReportSink("json", ReportDestinationType.File, "results.json"),
                    new ReportSink("sarif", ReportDestinationType.File, "results.sarif"),
                ],
                timing: timing);
        }

        using StringWriter timingReport = new();
        timing.WriteReport(timingReport);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
            Assert.That(result.RenderedFormats, Is.EquivalentTo(_allReportFormats));
            Assert.That(timingReport.ToString(), Does.Contain("render_human"));
            Assert.That(timingReport.ToString(), Does.Contain("render_json"));
            Assert.That(timingReport.ToString(), Does.Contain("render_sarif"));
            Assert.That(timingReport.ToString(), Does.Contain("output_staging"));
            Assert.That(timingReport.ToString(), Does.Contain("output_stream_write"));
            Assert.That(timingReport.ToString(), Does.Contain("output_commit"));
        });
    }
}
