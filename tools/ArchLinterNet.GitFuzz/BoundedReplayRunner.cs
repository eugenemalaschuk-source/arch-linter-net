using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArchLinterNet.GitFuzz;

// User-facing replay is always a separate process. The worker is intentionally not a public
// command: only this launcher supplies the process-memory envelope and the watchdog.
internal static partial class BoundedReplayRunner
{
    internal const string WorkerEnvironmentVariable = "ARCHLINTERNET_GIT_FUZZ_REPLAY_WORKER";
    internal const string WorkerReadyMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_READY";
    internal const string WorkerWarmupMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_WARMUP";
    internal const string WorkerCaseReadyMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_CASE_READY";
    internal const string WorkerStartMarker = "ARCHLINTERNET_GIT_FUZZ_REPLAY_GO";
    internal const int PerCaseTimeoutMilliseconds = 100;
    internal const int WorkerStartupTimeoutMilliseconds = 20_000;
    internal const long ProcessMemoryLimitBytes = 512L * 1024 * 1024;
    internal const string ManagedHeapHardLimit = "0x20000000";
    internal const string ReplayContainerImage =
        "mcr.microsoft.com/dotnet/runtime@sha256:a365ce6a50b09176855d085c69da3fc1204a48432e36087e9a208f6e5860e235";
    internal const int ReplayTimedOutExitCode = 124;
    internal const int ReplayLimitSetupExitCode = 125;
    private const string ContainerScratchDirectory = "/run/arch-linter-git-fuzz-replay";

    // Resolved once, to an absolute path, rather than launching "docker" and relying on
    // PATH lookup at every invocation.
    private static readonly Lazy<string> _resolvedDockerExecutablePath = new(() => ResolveExecutablePath("docker"));

    private static string ResolveExecutablePath(string executableName)
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return executableName;
        }

        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }

    internal static int Run(string inputPath)
        => Run(inputPath, Environment.ProcessPath);

    internal static int Run(string inputPath, string? processPath)
        => Run(CreateCommand(inputPath, processPath));

    internal static int Run(ReplayCommand command)
        => Run(command, PerCaseTimeoutMilliseconds);

    // The per-case timeout is only overridable from tests, so an end-to-end mechanism
    // check (does the bounded launcher complete a real round trip at all) can use a
    // relaxed bound instead of racing the coverage-instrumented worker against the
    // production 100 ms hang watchdog. BoundedReplayKillsAWorkerThatExceedsTheCaseWatchdog
    // is what actually proves the production PerCaseTimeoutMilliseconds enforces a kill.
    internal static int Run(ReplayCommand command, int perCaseTimeoutMilliseconds)
    {
        using Process process = Start(command);
        IDisposable memoryLimit;
        try
        {
            memoryLimit = AttachMemoryLimit(process);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Kill(process, command);
            throw new InvalidOperationException(
                "The bounded replay memory envelope could not be installed.",
                exception);
        }

        using (memoryLimit)
        {
            return RunBoundedWorker(process, command, perCaseTimeoutMilliseconds);
        }
    }

    private static int RunBoundedWorker(Process process, ReplayCommand command, int perCaseTimeoutMilliseconds)
    {
        if (!WaitForWorkerMarker(process, WorkerReadyMarker))
        {
            Kill(process, command);
            ForwardRemainingOutput(process);
            Console.Error.WriteLine(
                $"Bounded replay worker did not become ready within {WorkerStartupTimeoutMilliseconds} ms.");
            return ReplayTimedOutExitCode;
        }

        if (!TrySendWorkerMarker(process, WorkerWarmupMarker)
            || !WaitForWorkerMarker(process, WorkerCaseReadyMarker))
        {
            Kill(process, command);
            ForwardRemainingOutput(process);
            Console.Error.WriteLine(
                $"Bounded replay worker did not finish warm-up within {WorkerStartupTimeoutMilliseconds} ms.");
            return ReplayTimedOutExitCode;
        }

        if (!TrySendWorkerMarker(process, WorkerStartMarker))
        {
            Kill(process, command);
            ForwardRemainingOutput(process);
            Console.Error.WriteLine("Bounded replay worker exited before the candidate case started.");
            return ReplayTimedOutExitCode;
        }

        if (!process.WaitForExit(perCaseTimeoutMilliseconds))
        {
            Kill(process, command);
            Console.Error.WriteLine(
                $"Bounded replay exceeded the {perCaseTimeoutMilliseconds} ms per-case limit.");
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

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return CreateContainerCommand(inputPath, assemblyPath);
        }

        throw new PlatformNotSupportedException(
            "Bounded replay supports Windows, Linux, and macOS hosts only.");
    }

    private static ReplayCommand CreateContainerCommand(string inputPath, string assemblyPath)
    {
        string assemblyDirectory = Path.GetDirectoryName(assemblyPath)
            ?? throw new InvalidOperationException("The bounded replay launcher has no assembly directory.");
        string fullInputPath = Path.GetFullPath(inputPath);
        string inputDirectory = Path.GetDirectoryName(fullInputPath)
            ?? throw new InvalidOperationException("The bounded replay input has no parent directory.");
        string inputFileName = Path.GetFileName(fullInputPath);
        if (string.IsNullOrWhiteSpace(inputFileName))
        {
            throw new InvalidOperationException("The bounded replay input has no file name.");
        }

        string containerName = $"arch-linter-git-fuzz-{Guid.NewGuid():N}";
        List<string> containerArguments =
        [
            "run",
            "--rm",
            "--init",
            // The READY/WARMUP/CASE_READY/GO handshake runs over stdin/stdout; without -i, docker
            // does not forward the host process's stdin into the container, so the worker never
            // receives WARMUP/GO and the launcher fails closed on every Unix invocation.
            "-i",
            "--name",
            containerName,
            "--network",
            "none",
            "--memory=512m",
            "--memory-swap=512m",
            "--cpus=1",
            "--read-only",
            "--tmpfs",
            $"{ContainerScratchDirectory}:rw,nosuid,nodev,noexec,size=128m",
            // GitFuzz itself is excluded from coverage instrumentation (see
            // TEST_COVERAGE_COLLECTOR_EXCLUDE in make/test.mk), but ArchLinterNet.Core is a
            // runtime dependency copied alongside it into /harness, and Core IS legitimately
            // instrumented under the coverage-collecting CI job. .NET's named Mutex/Semaphore
            // support creates its lock files under a fixed, TMPDIR-independent path so
            // unrelated processes can rendezvous on the same name; Coverlet's tracker opens
            // one of these on module unload for whichever instrumented assembly is loaded.
            // This stays writable purely for that runtime requirement — the application
            // itself never reads or writes here (see ContainerScratchDirectory/TMPDIR above).
            "--tmpfs",
            "/tmp:rw,nosuid,nodev,noexec,size=16m",
            "--mount",
            $"type=bind,src={assemblyDirectory},dst=/harness,readonly",
            "--mount",
            $"type=bind,src={inputDirectory},dst=/input,readonly",
            "--workdir",
            ContainerScratchDirectory,
            "--env",
            $"{WorkerEnvironmentVariable}=1",
            "--env",
            $"DOTNET_GCHeapHardLimit={ManagedHeapHardLimit}",
            // A dedicated, non-well-known scratch directory rather than the shared "/tmp"
            // name: the corpus warm-up materializes files via Path.GetTempPath(), which
            // .NET resolves from TMPDIR on Unix.
            "--env",
            $"TMPDIR={ContainerScratchDirectory}",
            ReplayContainerImage,
        ];

        // Always run the managed assembly through the container's own dotnet runtime
        // (ReplayContainerImage), never the host's process name: on Unix, "dotnet run"/"dotnet
        // build" produce a native apphost (Mach-O on macOS, ELF on Linux) at that path unless
        // UseAppHost=false is set, and a macOS apphost cannot execute inside a Linux container
        // regardless of what CreateJobObject/IsDotNetHost concluded about the host process.
        containerArguments.Add("dotnet");
        containerArguments.Add($"/harness/{Path.GetFileName(assemblyPath)}");

        containerArguments.Add("--replay-worker");
        containerArguments.Add($"/input/{inputFileName}");
        return new ReplayCommand(
            _resolvedDockerExecutablePath.Value,
            containerArguments,
            UsesWindowsJobObject: false,
            ContainerName: containerName);
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
                + "Install Docker on Linux or macOS, or use the Windows replay launcher.",
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

    private static bool TrySendWorkerMarker(Process process, string marker)
    {
        try
        {
            process.StandardInput.WriteLine(marker);
            process.StandardInput.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static void ForwardRemainingOutput(Process process)
    {
        string standardError = process.StandardError.ReadToEnd();
        if (standardError.Length > 0)
        {
            Console.Error.Write(standardError);
        }
    }

    private static void Kill(Process process, ReplayCommand? command = null)
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

        try
        {
            process.WaitForExit();
        }
        finally
        {
            if (command?.ContainerName is not null)
            {
                RemoveContainer(command.Value.ContainerName);
            }
        }
    }

    private static void RemoveContainer(string containerName)
    {
        try
        {
            ProcessStartInfo startInfo = new(_resolvedDockerExecutablePath.Value)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("rm");
            startInfo.ArgumentList.Add("--force");
            startInfo.ArgumentList.Add(containerName);
            using Process cleanup = Process.Start(startInfo)!;
            _ = cleanup.WaitForExit(2_000);
        }
        catch (Win32Exception)
        {
            // The Docker launcher itself is unavailable; the main process already failed closed.
        }
    }

    private static bool IsDotNetHost(string processPath)
        => string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

    internal readonly record struct ReplayCommand(
        string FileName,
        IReadOnlyList<string> Arguments,
        bool UsesWindowsJobObject,
        string? ContainerName = null);

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

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObject(nint attributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        nint job,
        int informationClass,
        ref JobObjectExtendedLimitInformation information,
        uint informationLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint job, nint process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
