using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Issue #375 follow-up (PR #416 review): every baseline subcommand handler previously caught
// OperationCanceledException with its generic catch (Exception), reporting real cancellation as a
// "<command> error", and never re-checked the token between Core returning an outcome and the
// handler's own write/publish step. Shares BaselineCommandHandlerTests' StubRuntime/StubFileSystem/
// RecordingConsole fixtures via the partial class.
public sealed partial class BaselineCommandHandlerTests
{
    [Test]
    public void BaselineUpdate_CoreThrowsOperationCanceled_ReportsTypedCancelledStatusNotGenericError()
    {
        var runtime = new StubRuntime { UpdateException = new OperationCanceledException("cancelled") };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "updated.yml", _reasons, "strict", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Baseline update was cancelled."));
            Assert.That(console.ErrorText, Does.Not.Contain("Baseline update error"));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void BaselineUpdate_CoreThrowsOperationCanceled_JsonFormat_EmitsTypedCancelledStatus()
    {
        var runtime = new StubRuntime { UpdateException = new OperationCanceledException("cancelled") };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "updated.yml", _reasons, "strict", null, "json", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Does.Contain("\"status\":\"cancelled\""));
        });
    }

    // Reproduces the exact race the review flagged: Core successfully returns an outcome, but the
    // caller's token was cancelled in that same window (e.g. Ctrl+C landed right after Core's own
    // last check). Without a re-check immediately before BaselineWriteGate.TryApply, this would
    // write the baseline and report success despite the cancellation.
    [Test]
    public void BaselineUpdate_TokenCancelledAfterOutcomeReturned_DoesNotWriteAndReportsCancelled()
    {
        using CancellationTokenSource cts = new();
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "updated: yaml", 3, 1, Array.Empty<ArchitectureViolation>()),
            OnUpdateBaseline = () => cts.Cancel(),
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem, cts.Token).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "updated.yml", _reasons, "strict", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Baseline update was cancelled."));
            Assert.That(fileSystem.LastWritePath, Is.Null, "cancellation observed before TryApply must prevent the write entirely");
        });
    }

    [Test]
    public void BaselineGenerate_TokenCancelledAfterOutcomeReturned_DoesNotWriteAndReportsCancelled()
    {
        using CancellationTokenSource cts = new();
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "generated: yaml", 2, Array.Empty<ArchitectureViolation>()),
            OnGenerateBaseline = () => cts.Cancel(),
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem, cts.Token).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "strict", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Baseline generation was cancelled."));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void BaselineDiff_CoreThrowsOperationCanceled_ReportsTypedCancelledStatusNotGenericError()
    {
        var runtime = new StubRuntime { DiffException = new OperationCanceledException("cancelled") };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineDiffCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "all", null, "human", Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Baseline diff was cancelled."));
            Assert.That(console.ErrorText, Does.Not.Contain("Baseline diff error"));
        });
    }
}
