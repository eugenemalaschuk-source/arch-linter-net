using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Owns the lifetime of processes started by the Checkpoint B release-gate fixture.
/// This is intentionally test-only: production process execution has a separate contract.
/// </summary>
internal static partial class CheckpointBProcessRunner
{
    internal static readonly TimeSpan ProcessCompletionTimeout = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan PostExitDrainTimeout = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(2);

    private const int StreamBufferSize = 4096;
    private const int DiagnosticTailCharacterLimit = 4096;

    private static readonly char[] _charsRequiringQuoting = [' ', '\t', '\n', '\v', '"'];
    private static readonly UTF8Encoding _spoolEncoding = new(encoderShouldEmitUTF8Identifier: false);

    internal static CheckpointBReleaseGateTests.CommandResult Run(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        return RunAsync(startInfo, cancellationToken).GetAwaiter().GetResult();
    }

    private static async Task<CheckpointBReleaseGateTests.CommandResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        string command = RenderCommand(startInfo);
        Stopwatch elapsed = Stopwatch.StartNew();
        WindowsJobScope? job = OperatingSystem.IsWindows() ? new WindowsJobScope() : null;
        Process? process = null;
        Task? completion = null;
        Task? standardOutputTask = null;
        Task? standardErrorTask = null;
        StreamCapture? standardOutput = null;
        StreamCapture? standardError = null;
        StreamReader? standardOutputReader = null;
        StreamReader? standardErrorReader = null;

        try
        {
            // On Windows, the job list is attached to process creation itself (STARTUPINFOEX +
            // PROC_THREAD_ATTRIBUTE_JOB_LIST), so the root process is already contained before its
            // first instruction runs. There is no window between start and containment in which an
            // early descendant could be spawned outside the job, and no separate
            // AssignProcessToJobObject call whose failure could leave the root untracked.
            if (job is not null)
            {
                (process, standardOutputReader, standardErrorReader) = job.LaunchContained(startInfo);
            }
            else
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
                standardOutputReader = process.StandardOutput;
                standardErrorReader = process.StandardError;
            }

            int processId = process.Id;

            // Everything from here on runs against a live process: any exception, whether it is a
            // deliberate cancellation/timeout or a genuine fault from WaitForExitAsync or a stream
            // read, must trigger the same bounded best-effort cleanup and then propagate unchanged.
            // Cleanup itself must never be allowed to replace that original exception.
            try
            {
                standardOutput = new StreamCapture();
                standardError = new StreamCapture();
#pragma warning disable CA2016
                standardOutputTask = standardOutput.ReadAsync(standardOutputReader);
                standardErrorTask = standardError.ReadAsync(standardErrorReader);
#pragma warning restore CA2016
                completion = process.WaitForExitAsync();

                await AwaitPhaseAsync(
                        completion,
                        ProcessCompletionTimeout,
                        cancellationToken,
                        "process completion",
                        command,
                        processId,
                        elapsed,
                        standardOutput,
                        standardError)
                    .ConfigureAwait(false);

                Task streams = Task.WhenAll(standardOutputTask, standardErrorTask);
                await AwaitPhaseAsync(
                        streams,
                        PostExitDrainTimeout,
                        cancellationToken,
                        "post-exit stream drain",
                        command,
                        processId,
                        elapsed,
                        standardOutput,
                        standardError)
                    .ConfigureAwait(false);

                return new CheckpointBReleaseGateTests.CommandResult(
                    process.ExitCode,
                    standardOutput.ReadResult(),
                    standardError.ReadResult());
            }
            catch (Exception error)
            {
                ExceptionDispatchInfo capturedError = ExceptionDispatchInfo.Capture(error);
                await CleanupAfterFailureAsync(process, job, completion, standardOutputTask, standardErrorTask)
                    .ConfigureAwait(false);
                capturedError.Throw();
                throw; // Unreachable: capturedError.Throw() always throws.
            }
        }
        finally
        {
            if (standardOutputTask is not null)
            {
                ObserveFailure(standardOutputTask);
            }

            if (standardErrorTask is not null)
            {
                ObserveFailure(standardErrorTask);
            }

            if (completion is not null)
            {
                ObserveFailure(completion);
            }

            standardOutputReader?.Dispose();
            standardErrorReader?.Dispose();
            standardOutput?.Dispose();
            standardError?.Dispose();
            process?.Dispose();
            job?.Dispose();
        }
    }

    /// <summary>
    /// Awaits <paramref name="task"/> bounded by <paramref name="timeout"/> and
    /// <paramref name="cancellationToken"/>. A genuine fault from <paramref name="task"/> and a
    /// cancellation propagate unchanged; only this method's own timeout is rewritten into a
    /// <see cref="TimeoutException"/> carrying the bounded diagnostic.
    /// </summary>
    private static async Task AwaitPhaseAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string phase,
        string command,
        int processId,
        Stopwatch elapsed,
        StreamCapture standardOutput,
        StreamCapture standardError)
    {
        try
        {
            await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(BuildTimeoutDiagnostic(
                command,
                processId,
                phase,
                elapsed,
                standardOutput,
                standardError));
        }
    }

    private static async Task CleanupAfterFailureAsync(
        Process process,
        WindowsJobScope? job,
        Task? completion,
        Task? standardOutputTask,
        Task? standardErrorTask)
    {
        RequestCleanup(process, job);
        if (completion is not null)
        {
            await WaitBestEffortAsync(completion, CleanupTimeout).ConfigureAwait(false);
        }

        if (standardOutputTask is not null || standardErrorTask is not null)
        {
            Task streams = Task.WhenAll(
                standardOutputTask ?? Task.CompletedTask,
                standardErrorTask ?? Task.CompletedTask);
            await WaitBestEffortAsync(streams, CleanupTimeout).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits for <paramref name="task"/> up to <paramref name="timeout"/> without ever throwing:
    /// used only during best-effort cleanup, where a fault or a timeout on the task being cleaned
    /// up must never replace the original exception this runner is already propagating.
    /// </summary>
    internal static async Task WaitBestEffortAsync(Task task, TimeSpan timeout)
    {
        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch
        {
            // Intentionally swallowed: see summary.
        }
    }

    private static void RequestCleanup(Process process, WindowsJobScope? job)
    {
        if (OperatingSystem.IsWindows())
        {
            // Closing a kill-on-close job terminates descendants even if the root has already
            // exited, which is the inherited redirected-handle failure this runner guards.
            job?.Dispose();
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The root can exit between HasExited and Kill. Its descendants are then outside
            // the direct-tree fallback, but the bounded drain wait still guarantees return.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Preserve the deterministic timeout/cancellation result if the OS rejects a late
            // kill request; no cleanup path is allowed to become an unbounded wait.
        }
    }

    private static string BuildTimeoutDiagnostic(
        string command,
        int processId,
        string phase,
        Stopwatch elapsed,
        StreamCapture standardOutput,
        StreamCapture standardError)
    {
        return $"Checkpoint B process runner timed out during {phase}. "
            + $"Command: {command}; root PID: {processId}; "
            + $"elapsed duration: {elapsed.Elapsed.TotalMilliseconds:0} ms; "
            + $"stdout tail (bounded): {RenderTail(standardOutput.Tail)}; "
            + $"stderr tail (bounded): {RenderTail(standardError.Tail)}";
    }

    private static string RenderCommand(ProcessStartInfo startInfo) => BuildCommandLine(startInfo);

    private static string BuildCommandLine(ProcessStartInfo startInfo)
    {
        var commandLine = new StringBuilder();
        AppendArgument(commandLine, startInfo.FileName);
        if (startInfo.ArgumentList.Count > 0)
        {
            foreach (string argument in startInfo.ArgumentList)
            {
                commandLine.Append(' ');
                AppendArgument(commandLine, argument);
            }
        }
        else if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            commandLine.Append(' ').Append(startInfo.Arguments);
        }

        return commandLine.ToString();
    }

    private static void AppendArgument(StringBuilder commandLine, string argument)
    {
        if (argument.Length != 0 && argument.IndexOfAny(_charsRequiringQuoting) < 0)
        {
            commandLine.Append(argument);
            return;
        }

        commandLine.Append('"');
        int index = 0;
        while (index < argument.Length)
        {
            char current = argument[index++];
            if (current == '\\')
            {
                int backslashCount = 1;
                while (index < argument.Length && argument[index] == '\\')
                {
                    backslashCount++;
                    index++;
                }

                if (index == argument.Length)
                {
                    commandLine.Append('\\', backslashCount * 2);
                }
                else if (argument[index] == '"')
                {
                    commandLine.Append('\\', (backslashCount * 2) + 1);
                    commandLine.Append('"');
                    index++;
                }
                else
                {
                    commandLine.Append('\\', backslashCount);
                }
            }
            else if (current == '"')
            {
                commandLine.Append('\\');
                commandLine.Append('"');
            }
            else
            {
                commandLine.Append(current);
            }
        }

        commandLine.Append('"');
    }

    private static string RenderTail(string tail) => tail.Length == 0 ? "<empty>" : tail;

    private static void ObserveFailure(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Captures a redirected stream without holding its full content in memory: every chunk is
    /// appended to a delete-on-close temp-file spool and folded into a small bounded tail used for
    /// timeout diagnostics. The full content is only materialized (read back from the spool) on
    /// the successful completion path, where a caller actually needs it.
    /// </summary>
    private sealed class StreamCapture : IDisposable
    {
        private readonly object _gate = new();
        private readonly StringBuilder _tail = new();
        private readonly FileStream _spool;
        private readonly StreamWriter _spoolWriter;

        internal StreamCapture()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"checkpoint-b-process-runner-{Guid.NewGuid():N}.spool");
            _spool = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                StreamBufferSize,
                FileOptions.DeleteOnClose);
            _spoolWriter = new StreamWriter(_spool, _spoolEncoding, StreamBufferSize) { AutoFlush = false };
        }

        internal string Tail
        {
            get
            {
                lock (_gate)
                {
                    return _tail.ToString();
                }
            }
        }

        internal async Task ReadAsync(StreamReader reader)
        {
            char[] buffer = new char[StreamBufferSize];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                lock (_gate)
                {
                    _spoolWriter.Write(buffer, 0, read);
                    _tail.Append(buffer, 0, read);
                    if (_tail.Length > DiagnosticTailCharacterLimit)
                    {
                        _tail.Remove(0, _tail.Length - DiagnosticTailCharacterLimit);
                    }
                }
            }
        }

        internal string ReadResult()
        {
            lock (_gate)
            {
                _spoolWriter.Flush();
                _spool.Position = 0;
                using var reader = new StreamReader(
                    _spool,
                    _spoolEncoding,
                    detectEncodingFromByteOrderMarks: false,
                    StreamBufferSize,
                    leaveOpen: true);
                return reader.ReadToEnd();
            }
        }

        public void Dispose() => _spoolWriter.Dispose();
    }
}
