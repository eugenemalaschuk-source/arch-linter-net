using ArchLinterNet.Cli.Commands.Baseline;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class BaselineCommandHandlerTests
{
    [Test]
    public void BaselineHelpTexts_DocumentSarifFormatForComparisonCommands()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BaselineHelpTexts.HelpText, Does.Contain("--format <fmt>"));
            Assert.That(BaselineHelpTexts.DiffHelpText, Does.Contain("--format <fmt>"));
            Assert.That(BaselineHelpTexts.VerifyHelpText, Does.Contain("--format <fmt>"));
            Assert.That(BaselineHelpTexts.MigrateHelpText, Does.Contain("--format <fmt>"));
        });
    }

    [Test]
    public void BaselineComparisonCommands_RejectConflictingJsonAndFormatOptionsDuringParsing()
    {
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var diffConsole = new RecordingConsole();
        var verifyConsole = new RecordingConsole();
        var migrateConsole = new RecordingConsole();

        int diffResult = new DiffBaselineSubcommandModule()
            .CreateCommand(new StubRuntime(), diffConsole, fileSystem)
            .Parse(["--policy", "policy.yml", "--baseline", "baseline.yml", "--json", "--format", "sarif"])
            .Invoke();
        int verifyResult = new VerifyBaselineSubcommandModule()
            .CreateCommand(new StubRuntime(), verifyConsole, fileSystem)
            .Parse(["--policy", "policy.yml", "--baseline", "baseline.yml", "--json", "--format", "sarif"])
            .Invoke();
        int migrateResult = new MigrateBaselineSubcommandModule()
            .CreateCommand(new StubRuntime(), migrateConsole, fileSystem)
            .Parse(["--policy", "policy.yml", "--baseline", "baseline.yml", "--dry-run", "--json", "--format", "sarif"])
            .Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(diffResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(verifyResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(migrateResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(diffConsole.ErrorText, Does.Contain("--json cannot be combined with --format"));
            Assert.That(verifyConsole.ErrorText, Does.Contain("--json cannot be combined with --format"));
            Assert.That(migrateConsole.ErrorText, Does.Contain("--json cannot be combined with --format"));
        });
    }

    [Test]
    public void BaselineDiffHandler_ReportsUserSuppliedConflictAsInvalidFormat()
    {
        var console = new RecordingConsole();

        int result = new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "all", null, "conflict", [], false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Invalid format"));
        });
    }
}
