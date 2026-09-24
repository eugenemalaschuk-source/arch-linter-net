using System.CommandLine;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.History.Application;
using ArchLinterNet.Cli.Commands.History.EntryPoint;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class HistoryCommandDefinitionTests
{
    [Test]
    public void AnalyzeSubcommand_ReportOption_IsParsedAndReachesTheHandler()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new HistoryCommandDefinition(
            new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem())).Create());
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-def-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);

        try
        {
            int exitCode = root.Parse([
                "history", "analyze",
                "--repository", outsideAnyRepository,
                "--from", "HEAD", "--to", "HEAD",
                "--report", "json=report.json", "--report", "markdown=report.md",
            ]).Invoke();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"repository_not_found\""));
            });
        }
        finally
        {
            Directory.Delete(outsideAnyRepository, recursive: true);
        }
    }

    [Test]
    public void AnalyzeSubcommand_InvalidReportValue_FailsClosedWithParseError()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new HistoryCommandDefinition(
            new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem())).Create());

        int exitCode = root.Parse([
            "history", "analyze",
            "--from", "HEAD", "--to", "HEAD",
            "--report", "json",
        ]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Not.EqualTo(0));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.Contain("Invalid --report value"));
        });
    }

    [Test]
    public void AnalyzeSubcommand_HelpFlagShortCircuits()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new HistoryCommandDefinition(
            new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem())).Create());

        int exitCode = root.Parse(["history", "analyze", "-h"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("arch-linter-net history analyze"));
            Assert.That(console.Output, Does.Contain("--report"));
        });
    }

    [Test]
    public void HistoryCommandModule_ForwardsCancellationToAnalysisHandler()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new HistoryCommandModule().CreateCommand(
            null!, console, new ScaffoldTestFileSystem(), cancellation.Token));

        int exitCode = root.Parse([
            "history", "analyze", "--from", "HEAD", "--to", "HEAD", "--timings",
        ]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.Contain("cancelled"));
            Assert.That(console.ErrorOutput, Does.Contain("ingestion_calls=0"));
        });
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public TextWriter Out => new StringWriter(_output);

        public TextWriter Error => new StringWriter(_error);

        public string Output => _output.ToString();

        public string ErrorOutput => _error.ToString();
    }
}
