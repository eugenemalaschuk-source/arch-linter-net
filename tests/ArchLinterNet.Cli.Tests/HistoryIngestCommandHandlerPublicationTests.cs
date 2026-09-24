using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.History.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class HistoryIngestCommandHandlerTests
{
    [Test]
    public void ACommitFailureDoesNotPublishStreamsAndReportsEveryDestination()
    {
        string repository = CreateRepositoryWithOneCommit();
        try
        {
            FakeConsole console = new();
            ScaffoldTestFileSystem fileSystem = new();
            FailOnRenameFileSystem failingFileSystem = new(fileSystem, "report.md");

            int exitCode = new HistoryIngestCommandHandler(console, failingFileSystem).Execute(
                new HistoryIngestCommandOptions(
                    repository, "HEAD", "HEAD", "json", false,
                    ReportSinks: new[]
                    {
                        new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.Stdout),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.File, "report.md"),
                    }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.Output, Is.Empty, "Stream sinks must wait until every staged file is committed.");
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"report_publication_failed\""));
                Assert.That(console.ErrorOutput, Does.Contain("\"publicationStatus\": \"partial-output\""));
                Assert.That(console.ErrorOutput, Does.Contain("\"failed\": [\n    \"report.md\"\n  ]"));
                Assert.That(console.ErrorOutput, Does.Contain("\"committed\": [\n    \"report.json\"\n  ]"));
                Assert.That(console.ErrorOutput, Does.Contain("\"delivered\": []"));
                Assert.That(console.ErrorOutput, Does.Contain("\"uncommitted\": [\n    \"<stdout>\",\n    \"report.md\"\n  ]"));
                Assert.That(fileSystem.CommittedPaths, Is.EqualTo(new[] { "report.json" }));
            });
        }
        finally
        {
            DeleteRepositoryDirectory(repository);
        }
    }
}
