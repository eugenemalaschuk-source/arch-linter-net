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
    public void BaselineComparisonHandlers_RejectConflictingJsonAndFormatOptions()
    {
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var diffConsole = new RecordingConsole();
        var verifyConsole = new RecordingConsole();
        var migrateConsole = new RecordingConsole();

        int diffResult = new BaselineDiffCommandHandler(new StubRuntime(), diffConsole, fileSystem).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "all", null, "conflict", [], false));
        int verifyResult = new BaselineVerifyCommandHandler(new StubRuntime(), verifyConsole, fileSystem).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", null, "conflict", [], false));
        int migrateResult = new BaselineMigrateCommandHandler(new StubRuntime(), migrateConsole, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", null, null, "conflict", true, false, false));

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
}
