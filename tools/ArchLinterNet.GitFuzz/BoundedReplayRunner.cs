using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ArchLinterNet.GitFuzz;

// User-facing replay is always a separate process. The worker is intentionally not a public
// command: only this launcher supplies the process-memory envelope and the watchdog.
internal static class BoundedReplayRunner
{
    internal const string WorkerEnvironmentVariable = "ARCHLINTERNET_GIT_FUZZ_REPLAY_WORKER";
    internal const string WorkerReadyMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_READY";
    internal const string WorkerWarmupMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_WARMUP";
    internal const string WorkerCaseReadyMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_CASE_READY";
    internal const string WorkerStartMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_GO";
    internal const int PerCaseTimeoutMilliseconds = 100;
    internal const int WorkerStartupTimeoutMilliseconds = 5_000;
    internal const long ProcessMemoryLimitBytes = 512L * 1024 * 1024;
    internal const string ManagedHeapHardLimit = "0x20000000";
    private const long MacMemoryLimitKilobytes = ProcessMemoryLimitBytes / 1024;
    private static readonly string MacMemoryLauncherScript =
        "ulimit -v " + MacMemoryLimitKilobytes.ToString(CultureInfo.InvariantCulture)
        + " || exit 125; exec \"$0\" \"$@\"";
    internal const int ReplayTimedOutExitCode = 124;
    internal const int ReplayLimitSetupExitCode = 125;

    internal static int Run(string inputPath)
        => Run(inputPath, Environment.ProcessPath);

    internal static int Run(string inputPath, string? processPath)
        => Run(CreateCommand(inputPath, processPath));

    internal static int Run(ReplayCommand command)
    {
        using Process process = Start(command);
        IDisposable memoryLimit;
        try
        {
            memoryLimit = AttachMemoryLimit(process);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Kill(process);
            throw new InvalidOperationException(
                "The bounded replay memory envelope could not be installed.",
                exception);
        }

        using (memoryLimit)
        {
            return RunBoundedWorker(process);
        }
    }

    private static int RunBoundedWorker(Process process)
    {
        if (!WaitForWorkerMarker(process, WorkerReadyMarker))
        {
            Kill(process);
            Console.Error.WriteLine("Bounded replay worker did not become ready within 5 seconds.");
            return ReplayTimedOutExitCode;
        }

        process.StandardInput.WriteLine(WorkerWarmupMarker);
        process.StandardInput.Flush();
        if (!WaitForWorkerMarker(process, WorkerCaseReadyMarker))
        {
            Kill(process);
            Console.Error.WriteLine("Bounded replay worker did not finish warm-up within 5 seconds.");
            return ReplayTimedOutExitCode;
        }

        process.StandardInput.WriteLine(WorkerStartMarker);
        process.StandardInput.Flush();
        if (!process.WaitForExit(PerCaseTimeoutMilliseconds))
        {
            Kill(process);
            Console.Error.WriteLine(
                $"Bounded replay exceeded the {PerCaseTimeoutMilliseconds} ms per-case limit.");
            return ReplayTimedOutExitCode;
        }

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        if (standardOutput.Length > 0)
        {
            Console.Write(standardOutput);
        }

        if (standardError.Length > 0)
        {
            Console.Error.Write(standardError);
        }

        return process.ExitCode;
    }

    internal static ReplayCommand CreateCommand(string inputPath)
        => CreateCommand(inputPath, Environment.ProcessPath);

    internal static ReplayCommand CreateCommand(string inputPath, string? processPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        string resolvedProcessPath = processPath
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The bounded replay launcher has no process path.");
        string assemblyPath = typeof(Program).Assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new InvalidOperationException("The bounded replay launcher has no assembly path.");
        }

        List<string> workerArguments = [];
        if (IsDotNetHost(resolvedProcessPath))
        {
            workerArguments.Add(assemblyPath);
        }

        workerArguments.Add("--replay-worker");
        workerArguments.Add(Path.GetFullPath(inputPath));

        if (OperatingSystem.IsWindows())
        {
            return new ReplayCommand(resolvedProcessPath, workerArguments, UsesWindowsJobObject: true);
        }

        if (OperatingSystem.IsLinux())
        {
            List<string> prlimitArguments =
            [
                $"--as={ProcessMemoryLimitBytes.ToString(CultureInfo.InvariantCulture)}",
                "--",
                resolvedProcessPath,
                .. workerArguments,
            ];
            return new ReplayCommand("prlimit", prlimitArguments, UsesWindowsJobObject: false);
        }

        if (OperatingSystem.IsMacOS())
        {
            string[] shellArguments =
            [
                "-c",
                MacMemoryLauncherScript,
                resolvedProcessPath,
                .. workerArguments,
            ];
            return new ReplayCommand("/bin/sh", shellArguments, UsesWindowsJobObject: false);
        }

        throw new PlatformNotSupportedException(
            "Bounded replay supports Windows, Linux, and macOS hosts only.");
    }

    private static Process Start(ReplayCommand command)
    {
        ProcessStartInfo startInfo = new(command.FileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[WorkerEnvironmentVariable] = "1";
        startInfo.Environment["DOTNET_GCHeapHardLimit"] = ManagedHeapHardLimit;

        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("The bounded replay worker could not be started.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "The bounded replay memory launcher is unavailable on this host. "
                + "Install prlimit on Unix-like hosts or use Windows Job Objects.",
                exception);
        }
    }

    private static IDisposable AttachMemoryLimit(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return NoopDisposable.Instance;
        }

        nint job = CreateJobObject(nint.Zero, null);
        if (job == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject failed.");
        }

        try
        {
            JobObjectExtendedLimitInformation limits = new()
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitProcessMemory | JobObjectLimitKillOnJobClose,
                },
                ProcessMemoryLimit = (nuint)ProcessMemoryLimitBytes,
            };
            if (!SetInformationJobObject(
                    job,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject failed.");
            }

            if (!AssignProcessToJobObject(job, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed.");
            }

            return new JobHandle(job);
        }
        catch
        {
            CloseHandle(job);
            throw;
        }
    }

    private static bool WaitForWorkerMarker(Process process, string expectedMarker)
    {
        Task<string?> readyLine = process.StandardOutput.ReadLineAsync();
        try
        {
            string? line = readyLine.WaitAsync(TimeSpan.FromMilliseconds(WorkerStartupTimeoutMilliseconds))
                .GetAwaiter()
                .GetResult();
            return string.Equals(line, expectedMarker, StringComparison.Ordinal);
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The worker exited between the timeout check and the kill request.
        }

        process.WaitForExit();
    }

    private static bool IsDotNetHost(string processPath)
        => string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

    internal readonly record struct ReplayCommand(
        string FileName,
        IReadOnlyList<string> Arguments,
        bool UsesWindowsJobObject);

    private sealed class NoopDisposable : IDisposable
    {
        internal static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class JobHandle(nint handle) : IDisposable
    {
        public void Dispose() => CloseHandle(handle);
    }

    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitProcessMemory = 0x100;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        nint job,
        int informationClass,
        ref JobObjectExtendedLimitInformation information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);
}
