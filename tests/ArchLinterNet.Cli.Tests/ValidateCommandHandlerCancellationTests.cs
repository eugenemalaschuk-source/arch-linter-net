using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Split out of ValidateCommandHandlerReportModeTests.cs (which grew past the file-size lint
// threshold) — issue #375's cancellation completion-status coverage. Shares that file's private
// FakeCliRuntime/FakeCliConsole/FakeFileSystem fixtures via the partial class.
public sealed partial class ValidateCommandHandlerReportModeTests
{
    // Issue #375: cancellation exits via the same numeric category as any other execution error
    // (CliExitCodes.InvalidArgumentsOrRuntimeError) but must carry a distinct "cancelled"
    // status/kind — never the generic architecture_execution_error shape — in every configured
    // format, and route through file sinks the same way a pre-outcome execution error does.
    [Test]
    public void ValidateHandler_Cancelled_RoutesDistinctCancelledStatusToFileSink()
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
            Assert.That(fileSystem.CommittedPaths, Does.Contain("cancelled.json"));
            string committedContent = fileSystem.ReadAllText("cancelled.json.tmp");
            Assert.That(committedContent, Does.Contain("\"status\":\"cancelled\""));
            Assert.That(committedContent, Does.Not.Contain("architecture_execution_error"));
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
}
