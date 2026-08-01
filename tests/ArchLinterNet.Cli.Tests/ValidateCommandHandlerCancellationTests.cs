using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Split out of ValidateCommandHandlerReportModeTests.cs (which grew past the file-size lint
// threshold) — issue #375's cancellation completion-status coverage. Shares that file's private
// FakeCliRuntime/FakeCliConsole/FakeFileSystem fixtures via the partial class.
public sealed partial class ValidateCommandHandlerReportModeTests
{
    // Issue #375: cancellation exits via the same numeric category as any other execution error
    // (CliExitCodes.InvalidArgumentsOrRuntimeError) but must carry a distinct "cancelled"
    // status/kind — never the generic architecture_execution_error shape.
    //
    // PR #416 review round 2: WriteCancellation must NOT route through file sinks the way a
    // post-outcome execution error does — a --report file sink may already hold a legitimate
    // report from an earlier run, and #375 requires preserving pre-existing report destinations.
    // The typed cancelled notice goes to the safe stream fallback (stderr) instead, never
    // touching the configured file.
    [Test]
    public void ValidateHandler_Cancelled_NeverOverwritesConfiguredFileSink_UsesStderrFallback()
    {
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new OperationCanceledException("cancelled during validation")
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "cancelled.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Does.Not.Contain("cancelled.json"),
                "a --report file sink must never be overwritten by a cancellation notice — it may hold a legitimate report from an earlier run");
            Assert.That(console.StdErr, Does.Contain("\"status\":\"cancelled\""));
            Assert.That(console.StdErr, Does.Not.Contain("architecture_execution_error"));
        });
    }

    // PR #416 review: the test above passes the handler CancellationToken.None (the default) and
    // throws the OperationCanceledException artificially — it never exercises the actual
    // production path, where _cancellationToken is the SAME token that was cancelled and caused
    // Core to throw. This reproduces the real path: the handler's own token is cancelled, runtime
    // throws OCE from it, and the cancellation notice must still reach a safe fallback — and, per
    // round 2, must still never touch the configured file sink.
    [Test]
    public void ValidateHandler_HandlerTokenCancelled_RealProductionPath_NeverOverwritesFileSink()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new OperationCanceledException("cancelled during validation", cts.Token)
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem, cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "cancelled.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Does.Not.Contain("cancelled.json"));
            Assert.That(console.StdErr, Does.Contain("\"status\":\"cancelled\""));
            Assert.That(console.StdErr, Does.Not.Contain("output-failed"));
            Assert.That(console.StdErr, Does.Not.Contain("partial-output"));
        });
    }

    // PR #416 review round 2: BuildStateProcessCleanupTimedOutException carries ProcessId/TimeoutMs
    // evidence that a killed build/restore process never confirmed exit — the handler's generic
    // catch (OperationCanceledException) previously matched this subtype too and discarded that
    // evidence behind a bare "cancelled" message. A dedicated catch branch must surface it.
    [Test]
    public void ValidateHandler_ProcessCleanupTimedOut_SurfacesProcessIdAndTimeoutEvidence()
    {
        using CancellationTokenSource cts = new();
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new BuildStateProcessCleanupTimedOutException(4242, 5000, cts.Token)
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "json", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("\"status\":\"cancelled\""));
            Assert.That(console.StdOut, Does.Contain("\"processId\":4242"));
            Assert.That(console.StdOut, Does.Contain("\"processCleanupTimeoutMs\":5000"));
            Assert.That(console.StdOut, Does.Contain("\"processCleanupTimedOut\":true"));
        });
    }

    // Same production-path reproduction as above, for the legacy (no --report sinks) shape:
    // proves the stream fallback in WriteErrorContent also still delivers the typed cancelled
    // content — not a routing-failure document — when the handler's own token is cancelled.
    [Test]
    public void ValidateHandler_HandlerTokenCancelled_NoAdditionalSinks_WritesCancelledMessageToStderr()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new OperationCanceledException("cancelled during validation", cts.Token)
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem, cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Architecture validation was cancelled."));
            Assert.That(console.StdOut, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_Cancelled_NoAdditionalSinks_WritesCancelledMessageToStderr()
    {
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new OperationCanceledException("cancelled during validation")
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("cancelled"));
            Assert.That(console.StdOut, Is.Empty);
        });
    }

    [Test]
    public void ValidateHandler_Cancelled_SarifFormat_UsesDistinctCancelledRuleId()
    {
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new OperationCanceledException("cancelled during validation")
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        ValidateCommandHandler handler = new(runtime, console, fileSystem);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "sarif", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("\"ruleId\":\"architecture-cancelled\""));
            Assert.That(console.StdOut, Does.Not.Contain("architecture-execution"));
        });
    }

    // Issue #375 PR #416 review: proves cancellation reported by ReportCoordinator itself
    // (RouteResult.Cancelled — validation succeeded, staging/commit was interrupted) is handled
    // distinctly from an OperationCanceledException thrown by Core — the handler must not fall
    // through to WriteOutputError and report this as a generic "partial-output"/"output-failed".
    [Test]
    public void ValidateHandler_CoordinatorReportsCancelled_WritesDistinctCancelledStatusNotOutputFailure()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        using CancellationTokenSource cts = new();
        FakeFileSystem fileSystem = new(exists: true)
        {
            // The write itself succeeds; cancellation is simply already observed by the time
            // DistributeToSinks checks the token again before committing — not a write failure.
            OnWriteAllTextToTemp = () => cts.Cancel(),
        };
        ValidateCommandHandler handler = new(runtime, console, fileSystem, cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "out.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("\"status\":\"cancelled\""));
            Assert.That(console.StdErr, Does.Not.Contain("partial-output"));
            Assert.That(console.StdErr, Does.Not.Contain("output-failed"));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
        });
    }

    // SonarCloud S8949 (RELIABILITY, MAJOR): WriteErrorContent's call to
    // _coordinator.RouteErrorToAllSinks previously never forwarded _cancellationToken, so a
    // pre-outcome policy-load failure with --report file sinks configured would stage/commit
    // those sinks even when the handler's own token was already cancelled. Proves the token now
    // reaches RouteErrorToAllSinks: with it pre-cancelled, the file sink must never commit.
    [Test]
    public void ValidateHandler_PolicyErrorWithCancelledToken_RouteErrorToAllSinksHonorsToken()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/root.yml", ArchitecturePolicyDocumentRole.Root,
            0, null, null, ["architecture/root.yml"]);
        ArchitecturePolicySourceLocation location = new(source, "$", 1, 1, null, null);
        FakeCliRuntime runtime = new()
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Invalid namespace.",
                new ArchitecturePolicyDiagnostic(ArchitecturePolicyDiagnosticKind.SourceShape, location, [], source.ImportChain),
                ArchitecturePolicyImportErrorCategory.SourceShape.ToString())
        };
        FakeCliConsole console = new();
        FakeFileSystem fileSystem = new(exists: true);
        using CancellationTokenSource cts = new();
        cts.Cancel();
        ValidateCommandHandler handler = new(runtime, console, fileSystem, cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false)
        {
            AdditionalSinks = [new ReportSink("json", ReportDestinationType.File, "out.json")],
        };

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Does.Not.Contain("out.json"));
        });
    }

    // Coverage: WriteCancelledRouting's JSON branch is already exercised by
    // ValidateHandler_CoordinatorReportsCancelled_WritesDistinctCancelledStatusNotOutputFailure
    // (a --report json=... sink). These two cover the SARIF and human branches of the same
    // switch — ReportErrorContentFormatter.BuildCancelledOutputSarifText/HumanText — via the
    // legacy no-sinks path, which still goes through RouteResult.Cancelled (not an
    // OperationCanceledException) because RouteOutcomes' pre-render check fires before any
    // rendering happens for a token already cancelled at Execute() time.
    [Test]
    public void ValidateHandler_CoordinatorReportsCancelled_SarifFormat_UsesDistinctCancelledRuleId()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true), cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "sarif", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut, Does.Contain("\"ruleId\":\"architecture-cancelled\""));
            Assert.That(console.StdOut, Does.Contain("\"status\":\"cancelled\""));
        });
    }

    [Test]
    public void ValidateHandler_CoordinatorReportsCancelled_HumanFormat_WritesCancelledMessageToStderr()
    {
        FakeCliRuntime runtime = new();
        FakeCliConsole console = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true), cts.Token);

        ValidateCommandOptions options = new(
            "policy.yml", "strict", "human", [], null, false, null, false, false);

        int exitCode = handler.Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Architecture validation was cancelled during report output."));
        });
    }

}
