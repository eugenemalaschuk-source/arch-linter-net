using ArchLinterNet.Cli.Commands.PublicApi;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Issue #375 follow-up (PR #416 review): every public-api subcommand handler previously caught
// OperationCanceledException with its generic catch (Exception), reporting real cancellation as a
// "public-api <command> error", and never re-checked the token between Core returning an outcome
// and the handler's own temp-write/rename publish step. Shares PublicApiCommandHandlerTests'
// StubRuntime/StubFileSystem/RecordingConsole fixtures via the partial class.
public sealed partial class PublicApiCommandHandlerTests
{
    [Test]
    public void Update_CoreThrowsOperationCanceled_ReportsTypedCancelledStatusNotGenericError()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new() { UpdateException = new OperationCanceledException("cancelled") };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("public-api update was cancelled."));
            Assert.That(console.ErrorText, Does.Not.Contain("public-api update error"));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void Update_CoreThrowsOperationCanceled_JsonFormat_EmitsTypedCancelledStatus()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new() { UpdateException = new OperationCanceledException("cancelled") };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Does.Contain("\"status\":\"cancelled\""));
        });
    }

    // Reproduces the exact race the review flagged: Core successfully returns an outcome, but the
    // caller's token was cancelled in that same window. Without a re-check immediately before
    // WriteAllTextToTemp, this would commit the snapshot and report success despite cancellation —
    // the review's "signal between temp-write and rename" concern, closed at its source by never
    // starting the two-phase publish once cancellation is observed.
    [Test]
    public void Update_TokenCancelledAfterOutcomeReturned_DoesNotWriteAndReportsCancelled()
    {
        using CancellationTokenSource cts = new();
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, DriftDelta(), false, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
            OnUpdatePublicApi = () => cts.Cancel(),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem, cts.Token).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("public-api update was cancelled."));
            Assert.That(
                fileSystem.LastWritePath, Is.Null,
                "cancellation observed before WriteAllTextToTemp must prevent the write entirely — the existing snapshot stays untouched");
        });
    }

    // A dry run never reaches the publish step at all — cancellation observed after Core returns
    // must still be reported distinctly rather than falling through to a normal dry-run preview.
    [Test]
    public void Update_TokenCancelledAfterOutcomeReturned_DryRun_ReportsCancelledNotPreview()
    {
        using CancellationTokenSource cts = new();
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, DriftDelta(), true, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
            OnUpdatePublicApi = () => cts.Cancel(),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem, cts.Token).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", true, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("public-api update was cancelled."));
        });
    }

    [Test]
    public void Capture_TokenCancelledAfterOutcomeReturned_DoesNotWriteAndReportsCancelled()
    {
        using CancellationTokenSource cts = new();
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(true, CapturedSnapshot, 12, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
            OnCapturePublicApi = () => cts.Cancel(),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem, cts.Token).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("public-api capture was cancelled."));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void Diff_CoreThrowsOperationCanceled_ReportsTypedCancelledStatusNotGenericError()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new() { DiffException = new OperationCanceledException("cancelled") };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("public-api diff was cancelled."));
            Assert.That(console.ErrorText, Does.Not.Contain("public-api diff error"));
        });
    }
}
