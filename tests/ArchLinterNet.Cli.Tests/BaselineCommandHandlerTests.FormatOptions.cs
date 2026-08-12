using System.Text.Json;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.BuildState;
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
            AssertJsonError(diffConsole, "--json cannot be combined with --format");
            AssertJsonError(verifyConsole, "--json cannot be combined with --format");
            AssertJsonError(migrateConsole, "--json cannot be combined with --format");
        });
    }

    [Test]
    public void BaselineVerify_ForwardsBuildStateSelectors()
    {
        var runtime = new StubRuntime();
        var console = new RecordingConsole();
        int result = new VerifyBaselineSubcommandModule()
            .CreateCommand(runtime, console, new StubFileSystem("policy.yml", "baseline.yml"))
            .Parse([
                "--policy", "policy.yml", "--baseline", "baseline.yml",
                "--ensure-built", "--no-restore", "--configuration", "Release",
                "--framework", "net10.0", "--platform", "AnyCPU", "--runtime", "linux-x64",
            ])
            .Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.VerifyRequest, Is.Not.Null);
            Assert.That(runtime.VerifyRequest!.PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            Assert.That(runtime.VerifyRequest.NoRestore, Is.True);
            Assert.That(runtime.VerifyRequest.RequestedConfiguration, Is.EqualTo("Release"));
            Assert.That(runtime.VerifyRequest.RequestedTargetFramework, Is.EqualTo("net10.0"));
            Assert.That(runtime.VerifyRequest.RequestedPlatform, Is.EqualTo("AnyCPU"));
            Assert.That(runtime.VerifyRequest.RequestedRuntimeIdentifier, Is.EqualTo("linux-x64"));
        });
    }

    private static void AssertJsonError(RecordingConsole console, string message)
    {
        Assert.That(console.ErrorText, Is.Empty);
        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain(message));
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
