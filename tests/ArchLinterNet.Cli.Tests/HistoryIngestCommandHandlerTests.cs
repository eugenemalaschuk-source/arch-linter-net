using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.History.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class HistoryIngestCommandHandlerTests
{
    [Test]
    public void MissingOperandsFailWithoutTouchingTheRepository()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console).Execute(new HistoryIngestCommandOptions(".", string.Empty, "HEAD", "json", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.Contain("--from"));
        });
    }

    [Test]
    public void AnUnsupportedFormatIsRejected()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console).Execute(new HistoryIngestCommandOptions(".", "HEAD", "HEAD", "yaml", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
        });
    }

    // The fail-closed rule at the command boundary: a diagnostic goes to the error stream and the
    // output stream stays completely empty.
    [Test]
    public void AFailClosedRunWritesADiagnosticAndNoResult()
    {
        FakeConsole console = new();
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console).Execute(
                new HistoryIngestCommandOptions(outsideAnyRepository, "HEAD", "HEAD", "json", false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
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
    public void HelpWritesUsageAndSucceeds()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console).Execute(new HistoryIngestCommandOptions(".", string.Empty, string.Empty, "json", true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("arch-linter-net history ingest"));
            Assert.That(console.Output, Does.Contain("--policy <path>"));
        });
    }

    [Test]
    public void InvalidSelectedPolicyFailsBeforeRepositoryIngestion()
    {
        FakeConsole console = new();
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-history-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string policyPath = Path.Combine(directory, "policy.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: invalid history policy
            layers: {}
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            history_analysis:
              thresholds:
                co_change_significance: 1.000000001
            """);

        try
        {
            int exitCode = new HistoryIngestCommandHandler(console).Execute(
                new HistoryIngestCommandOptions(directory, "HEAD", "HEAD", "json", false, policyPath));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"configuration_invalid\""));
                Assert.That(console.ErrorOutput, Does.Contain("co_change_significance"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
