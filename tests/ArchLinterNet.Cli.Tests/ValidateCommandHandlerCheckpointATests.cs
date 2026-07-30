using ArchLinterNet.Cli.Commands.Validate;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class ValidateCommandHandlerReportModeTests
{
    [Test]
    public void CheckpointA_HumanJsonAndSarifSinks_ExecuteOneAnalysis()
    {
        var runtime = new FakeCliRuntime();
        var console = new FakeCliConsole();
        var fileSystem = new FakeFileSystem(exists: true);
        var handler = new ValidateCommandHandler(runtime, console, fileSystem);
        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks =
            [
                new ReportSink("human", ReportDestinationType.Stdout, null),
                new ReportSink("json", ReportDestinationType.File, "result.json"),
                new ReportSink("sarif", ReportDestinationType.File, "result.sarif"),
            ],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidationCallCount, Is.EqualTo(1));
            Assert.That(console.StdOut, Does.Contain("Architecture validation passed."));
            Assert.That(fileSystem.CommittedPaths, Is.EquivalentTo(new[] { "result.json", "result.sarif" }));
        });
    }
}
