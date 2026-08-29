using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class ReportCoordinatorTests
{
    [Test]
    public void RouteSingleOutcome_WaiversAreIncludedInHumanAndJsonOutput()
    {
        ValidationOutcome outcome = PassedOutcome with
        {
            Waivers =
            [
                new ArchitectureWaiverLifecycleRecord(
                    "ARCH-IGN-001", "expired", "boundary", "boundary", "strict", "App.Legacy",
                    "Infrastructure.Db", "sha256:" + new string('a', 64), "Legacy extraction", "architecture-team",
                    "ARCH-231", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), false)
            ]
        };
        var runtime = new CountingRuntime();
        var humanConsole = new CapturingConsole();
        var jsonConsole = new CapturingConsole();

        RouteResult human = new ReportCoordinator(runtime, humanConsole, new StubFileSystem())
            .RouteSingleOutcome("human", "strict", outcome, []);
        RouteResult json = new ReportCoordinator(runtime, jsonConsole, new StubFileSystem())
            .RouteSingleOutcome("json", "strict", outcome, []);

        using JsonDocument document = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(human.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
            Assert.That(json.Status, Is.EqualTo(ReportRouteStatus.AllSucceeded));
            Assert.That(humanConsole.OutputText, Does.Contain("Architecture waivers:"));
            Assert.That(humanConsole.OutputText, Does.Contain("[expired] ARCH-IGN-001"));
            Assert.That(humanConsole.OutputText, Does.Contain("target: sha256:"));
            Assert.That(humanConsole.OutputText, Does.Contain("reason: Legacy extraction"));
            Assert.That(document.RootElement.GetProperty("waivers")[0]!.GetProperty("id").GetString(), Is.EqualTo("ARCH-IGN-001"));
        });
    }
}
