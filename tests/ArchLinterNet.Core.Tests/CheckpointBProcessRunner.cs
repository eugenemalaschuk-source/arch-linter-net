using System.Diagnostics;
using System.Runtime.InteropServices;
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

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
            int processId = process.Id;

            // The job is created before start and assigned at the first possible point after
            // start, so descendants created by the Checkpoint B command remain in its scope.
            job?.Assign(process);

            standardOutput = new StreamCapture();
            standardError = new StreamCapture();
#pragma warning disable CA2016
            standardOutputTask = standardOutput.ReadAsync(process.StandardOutput);
            standardErrorTask = standardError.ReadAsync(process.StandardError);
#pragma warning restore CA2016
            completion = process.WaitForExitAsync();

            WaitOutcome processOutcome = await WaitBoundedAsync(
                    completion,
                    ProcessCompletionTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (processOutcome == WaitOutcome.Canceled)
            {
                await CleanupAfterFailureAsync(process, job, completion, standardOutputTask, standardErrorTask)
                    .ConfigureAwait(false);
                throw new OperationCanceledException(cancellationToken);
            }

            if (processOutcome == WaitOutcome.TimedOut)
            {
                await CleanupAfterFailureAsync(process, job, completion, standardOutputTask, standardErrorTask)
                    .ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new TimeoutException(BuildTimeoutDiagnostic(
                    command,
                    processId,
                    "process completion",
                    elapsed,
                    standardOutput,
                    standardError));
            }

            Task streams = Task.WhenAll(standardOutputTask, standardErrorTask);
            WaitOutcome drainOutcome = await WaitBoundedAsync(
                    streams,
                    PostExitDrainTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (drainOutcome != WaitOutcome.Completed)
            {
                await CleanupAfterFailureAsync(process, job, completion, standardOutputTask, standardErrorTask)
                    .ConfigureAwait(false);
                if (drainOutcome == WaitOutcome.Canceled || cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new TimeoutException(BuildTimeoutDiagnostic(
                    command,
                    processId,
                    "post-exit stream drain",
                    elapsed,
                    standardOutput,
                    standardError));
            }

            await streams.ConfigureAwait(false);
            return new CheckpointBReleaseGateTests.CommandResult(
                process.ExitCode,
                standardOutput.Result,
                standardError.Result);
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

            process?.Dispose();
            job?.Dispose();
        }
    }

    private static async Task<WaitOutcome> WaitBoundedAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Task timeoutTask = Task.Delay(timeout);
        Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completed = await Task.WhenAny(task, timeoutTask, cancellationTask).ConfigureAwait(false);
        if (completed == task)
        {
            await task.ConfigureAwait(false);
            return WaitOutcome.Completed;
        }

        return completed == cancellationTask ? WaitOutcome.Canceled : WaitOutcome.TimedOut;
    }

    private static async Task CleanupAfterFailureAsync(
        Process process,
        WindowsJobScope? job,
        Task completion,
        Task standardOutput,
        Task standardError)
    {
        RequestCleanup(process, job);
        _ = await WaitBoundedAsync(completion, CleanupTimeout, CancellationToken.None).ConfigureAwait(false);
        _ = await WaitBoundedAsync(
                Task.WhenAll(standardOutput, standardError),
                CleanupTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);
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

    private static string RenderCommand(ProcessStartInfo startInfo)
    {
        var parts = new List<string> { startInfo.FileName };
        if (startInfo.ArgumentList.Count > 0)
        {
            parts.AddRange(startInfo.ArgumentList.Select(RenderArgument));
        }
        else if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            parts.Add(startInfo.Arguments);
        }

        return string.Join(' ', parts);
    }

    private static string RenderArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) || argument.Contains('"', StringComparison.Ordinal)
            ? $"\"{argument.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
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

    private enum WaitOutcome
    {
        Completed,
        TimedOut,
        Canceled,
    }

    private sealed class StreamCapture
    {
        private readonly object _gate = new();
        private readonly StringBuilder _all = new();
        private readonly StringBuilder _tail = new();

        internal string Result
        {
            get
            {
                lock (_gate)
                {
                    return _all.ToString();
                }
            }
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
                    _all.Append(buffer, 0, read);
                    _tail.Append(buffer, 0, read);
                    if (_tail.Length > DiagnosticTailCharacterLimit)
                    {
                        _tail.Remove(0, _tail.Length - DiagnosticTailCharacterLimit);
                    }
                }
            }
        }
    }

    private sealed partial class WindowsJobScope : IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectExtendedLimitInformationClass = 9;

        private nint _handle;

        internal WindowsJobScope()
        {
            _handle = NativeMethods.CreateJobObject(0, null);
            if (_handle == 0)
            {
                throw NativeFailure("create the Checkpoint B process job");
            }

            var limits = new JobObjectExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            if (!NativeMethods.SetInformationJobObject(
                    _handle,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.CloseHandle(_handle);
                _handle = 0;
                throw NativeFailure("configure the Checkpoint B process job", error);
            }
        }

        internal void Assign(Process process)
        {
            if (!NativeMethods.AssignProcessToJobObject(_handle, process.Handle))
            {
                throw NativeFailure("attach the Checkpoint B root process to its job");
            }
        }

        public void Dispose()
        {
            nint handle = Interlocked.Exchange(ref _handle, 0);
            if (handle != 0)
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        private static InvalidOperationException NativeFailure(string action, int? error = null)
        {
            int win32Error = error ?? Marshal.GetLastWin32Error();
            return new InvalidOperationException($"Could not {action} (Win32 error {win32Error}).");
        }

        private static partial class NativeMethods
        {
            [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            internal static partial nint CreateJobObject(nint attributes, string? name);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool SetInformationJobObject(
                nint job,
                int informationClass,
                ref JobObjectExtendedLimitInformation information,
                uint informationLength);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool AssignProcessToJobObject(nint job, nint process);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool CloseHandle(nint handle);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            internal JobObjectBasicLimitInformation BasicLimitInformation;
            internal IoCounters IoInfo;
            internal nuint ProcessMemoryLimit;
            internal nuint JobMemoryLimit;
            internal nuint PeakProcessMemoryUsed;
            internal nuint PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            internal long PerProcessUserTimeLimit;
            internal long PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal nuint MinimumWorkingSetSize;
            internal nuint MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal nuint Affinity;
            internal uint PriorityClass;
            internal uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            internal ulong ReadOperationCount;
            internal ulong WriteOperationCount;
            internal ulong OtherOperationCount;
            internal ulong ReadTransferCount;
            internal ulong WriteTransferCount;
            internal ulong OtherTransferCount;
        }
    }
}
