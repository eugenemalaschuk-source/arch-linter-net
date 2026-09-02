using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.Tests;

internal static partial class CheckpointBProcessRunner
{
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint Infinite = 0xFFFFFFFF;
    private static readonly nint InvalidHandleValue = new(-1);
    private static readonly nuint ProcThreadAttributeHandleList = 0x00020002;
    private static readonly nuint ProcThreadAttributeJobList = 0x0002000D;

    private static StartedProcess StartWindowsProcess(
        ProcessStartInfo startInfo,
        WindowsJobScope job)
    {
        nint standardOutputRead = 0;
        nint standardOutputWrite = 0;
        nint standardErrorRead = 0;
        nint standardErrorWrite = 0;
        nint standardInput = 0;
        nint commandLine = 0;
        nint environment = 0;
        ProcessInformation processInformation = default;
        Process? process = null;
        StreamReader? standardOutput = null;
        StreamReader? standardError = null;
        SuspendedThread? suspendedThread = null;

        try
        {
            SecurityAttributes securityAttributes = CreateInheritableSecurityAttributes();
            CreateRedirectedPipe(ref securityAttributes, out standardOutputRead, out standardOutputWrite);
            CreateRedirectedPipe(ref securityAttributes, out standardErrorRead, out standardErrorWrite);
            standardInput = NativeMethods.CreateFile(
                "NUL",
                GenericRead,
                FileShareRead | FileShareWrite,
                ref securityAttributes,
                OpenExisting,
                FileAttributeNormal,
                0);
            if (standardInput == InvalidHandleValue)
            {
                standardInput = 0;
                throw NativeFailure("open the Checkpoint B null stdin handle");
            }

            using var attributes = new ProcThreadAttributeList(attributeCount: 2);
            attributes.Add(ProcThreadAttributeHandleList, standardInput, standardOutputWrite, standardErrorWrite);
            attributes.Add(ProcThreadAttributeJobList, job.Handle);

            var startup = new StartupInfoEx();
            startup.StartupInfo.Size = (uint)Marshal.SizeOf<StartupInfoEx>();
            startup.StartupInfo.Flags = StartfUseStdHandles;
            startup.StartupInfo.StandardInput = standardInput;
            startup.StartupInfo.StandardOutput = standardOutputWrite;
            startup.StartupInfo.StandardError = standardErrorWrite;
            startup.AttributeList = attributes.Handle;

            commandLine = Marshal.StringToHGlobalUni(BuildWindowsCommandLine(startInfo));
            environment = Marshal.StringToHGlobalUni(BuildWindowsEnvironmentBlock(startInfo));
            uint creationFlags = CreateSuspended | CreateUnicodeEnvironment | ExtendedStartupInfoPresent;
            if (startInfo.CreateNoWindow)
            {
                creationFlags |= CreateNoWindow;
            }

            if (!NativeMethods.CreateProcess(
                    applicationName: null,
                    commandLine,
                    processAttributes: 0,
                    threadAttributes: 0,
                    inheritHandles: true,
                    creationFlags,
                    environment,
                    string.IsNullOrWhiteSpace(startInfo.WorkingDirectory) ? null : startInfo.WorkingDirectory,
                    ref startup,
                    out processInformation))
            {
                throw NativeFailure($"start '{startInfo.FileName}' inside the Checkpoint B process job");
            }

            CloseHandle(ref standardOutputWrite);
            CloseHandle(ref standardErrorWrite);
            CloseHandle(ref standardInput);

            process = Process.GetProcessById(checked((int)processInformation.ProcessId));
            _ = process.SafeHandle;
            standardOutput = CreateReader(
                ref standardOutputRead,
                startInfo.StandardOutputEncoding ?? Console.OutputEncoding);
            standardError = CreateReader(
                ref standardErrorRead,
                startInfo.StandardErrorEncoding ?? Console.OutputEncoding);
            suspendedThread = new SuspendedThread(processInformation.Thread);
            processInformation.Thread = 0;
            CloseHandle(ref processInformation.Process);

            var result = new StartedProcess(
                process,
                standardOutput,
                standardError,
                suspendedThread.Resume,
                suspendedThread);
            process = null;
            standardOutput = null;
            standardError = null;
            suspendedThread = null;
            return result;
        }
        catch
        {
            if (processInformation.Process != 0)
            {
                _ = NativeMethods.TerminateProcess(processInformation.Process, 1);
                _ = NativeMethods.WaitForSingleObject(processInformation.Process, CleanupTimeoutMilliseconds);
            }

            throw;
        }
        finally
        {
            process?.Dispose();
            standardOutput?.Dispose();
            standardError?.Dispose();
            suspendedThread?.Dispose();
            CloseHandle(ref processInformation.Thread);
            CloseHandle(ref processInformation.Process);
            CloseHandle(ref standardOutputRead);
            CloseHandle(ref standardOutputWrite);
            CloseHandle(ref standardErrorRead);
            CloseHandle(ref standardErrorWrite);
            CloseHandle(ref standardInput);
            if (commandLine != 0)
            {
                Marshal.FreeHGlobal(commandLine);
            }

            if (environment != 0)
            {
                Marshal.FreeHGlobal(environment);
            }
        }
    }

    private static uint CleanupTimeoutMilliseconds => checked((uint)CleanupTimeout.TotalMilliseconds);

    private static SecurityAttributes CreateInheritableSecurityAttributes() => new()
    {
        Size = (uint)Marshal.SizeOf<SecurityAttributes>(),
        InheritHandle = 1,
    };

    private static void CreateRedirectedPipe(
        ref SecurityAttributes securityAttributes,
        out nint read,
        out nint write)
    {
        if (!NativeMethods.CreatePipe(out read, out write, ref securityAttributes, 0))
        {
            throw NativeFailure("create a Checkpoint B redirected stream pipe");
        }

        if (!NativeMethods.SetHandleInformation(read, HandleFlagInherit, 0))
        {
            int error = Marshal.GetLastWin32Error();
            CloseHandle(ref read);
            CloseHandle(ref write);
            throw NativeFailure("make the Checkpoint B parent pipe handle non-inheritable", error);
        }
    }

    private static StreamReader CreateReader(ref nint handle, Encoding encoding)
    {
        var safeHandle = new SafeFileHandle(handle, ownsHandle: true);
        handle = 0;
        var stream = new FileStream(safeHandle, FileAccess.Read, StreamBufferSize, isAsync: true);
        return new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            StreamBufferSize,
            leaveOpen: false);
    }

    private static string BuildWindowsCommandLine(ProcessStartInfo startInfo)
    {
        var commandLine = new StringBuilder(QuoteWindowsArgument(startInfo.FileName));
        if (IsCommandInterpreterInvocation(startInfo))
        {
            // cmd.exe parses the text following /c itself rather than through the CRT argv
            // convention. Quoting the complete command as an ordinary ArgumentList item turns
            // embedded quotes into syntax and can keep the root interpreter alive indefinitely.
            commandLine.Append(' ').Append(startInfo.ArgumentList[0]);
            commandLine.Append(' ').Append(startInfo.ArgumentList[1]);
            return commandLine.ToString();
        }

        if (startInfo.ArgumentList.Count > 0)
        {
            foreach (string argument in startInfo.ArgumentList)
            {
                commandLine.Append(' ').Append(QuoteWindowsArgument(argument));
            }
        }
        else if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            commandLine.Append(' ').Append(startInfo.Arguments);
        }

        return commandLine.ToString();
    }

    private static bool IsCommandInterpreterInvocation(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count != 2)
        {
            return false;
        }

        string executable = Path.GetFileName(startInfo.FileName);
        string commandSwitch = startInfo.ArgumentList[0];
        bool isCommandInterpreter = executable.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || executable.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase);
        return isCommandInterpreter
            && (commandSwitch.Equals("/c", StringComparison.OrdinalIgnoreCase)
                || commandSwitch.Equals("/k", StringComparison.OrdinalIgnoreCase));
    }

    private static string QuoteWindowsArgument(string argument)
    {
        if (argument.Length > 0 && !argument.Any(static character => char.IsWhiteSpace(character) || character == '"'))
        {
            return argument;
        }

        var quoted = new StringBuilder(argument.Length + 2).Append('"');
        int backslashCount = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', (backslashCount * 2) + 1).Append('"');
                backslashCount = 0;
                continue;
            }

            quoted.Append('\\', backslashCount).Append(character);
            backslashCount = 0;
        }

        quoted.Append('\\', backslashCount * 2).Append('"');
        return quoted.ToString();
    }

    private static string BuildWindowsEnvironmentBlock(ProcessStartInfo startInfo)
    {
        IEnumerable<string> entries = startInfo.Environment
            .Where(static pair => pair.Value is not null)
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => $"{pair.Key}={pair.Value}");
        return string.Join('\0', entries) + "\0\0";
    }

    private static void CloseHandle(ref nint handle)
    {
        nint value = Interlocked.Exchange(ref handle, 0);
        if (value != 0 && value != InvalidHandleValue)
        {
            _ = NativeMethods.CloseHandle(value);
        }
    }

    private static InvalidOperationException NativeFailure(string action, int? error = null)
    {
        int win32Error = error ?? Marshal.GetLastWin32Error();
        return new InvalidOperationException($"Could not {action} (Win32 error {win32Error}).");
    }

    private sealed class SuspendedThread : IDisposable
    {
        private nint _handle;

        internal SuspendedThread(nint handle)
        {
            _handle = handle;
        }

        internal void Resume()
        {
            nint handle = Interlocked.Exchange(ref _handle, 0);
            if (handle == 0)
            {
                return;
            }

            try
            {
                if (NativeMethods.ResumeThread(handle) == Infinite)
                {
                    throw NativeFailure("resume the job-owned Checkpoint B root process");
                }
            }
            finally
            {
                _ = NativeMethods.CloseHandle(handle);
            }
        }

        public void Dispose()
        {
            CloseHandle(ref _handle);
        }
    }

    private sealed class ProcThreadAttributeList : IDisposable
    {
        private readonly List<nint> _values = new();
        private nint _handle;

        internal ProcThreadAttributeList(int attributeCount)
        {
            nuint size = 0;
            _ = NativeMethods.InitializeProcThreadAttributeList(0, attributeCount, 0, ref size);
            if (size == 0)
            {
                throw NativeFailure("size the Checkpoint B process attribute list");
            }

            _handle = Marshal.AllocHGlobal(checked((nint)size));
            if (!NativeMethods.InitializeProcThreadAttributeList(_handle, attributeCount, 0, ref size))
            {
                int error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(_handle);
                _handle = 0;
                throw NativeFailure("initialize the Checkpoint B process attribute list", error);
            }
        }

        internal nint Handle => _handle != 0
            ? _handle
            : throw new ObjectDisposedException(nameof(ProcThreadAttributeList));

        internal void Add(nuint attribute, params nint[] values)
        {
            nint valueBuffer = Marshal.AllocHGlobal(checked(values.Length * nint.Size));
            for (int index = 0; index < values.Length; index++)
            {
                Marshal.WriteIntPtr(valueBuffer, index * nint.Size, values[index]);
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                    Handle,
                    0,
                    attribute,
                    valueBuffer,
                    checked((nuint)(values.Length * nint.Size)),
                    0,
                    0))
            {
                int error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(valueBuffer);
                throw NativeFailure("configure a Checkpoint B process attribute", error);
            }

            _values.Add(valueBuffer);
        }

        public void Dispose()
        {
            nint handle = Interlocked.Exchange(ref _handle, 0);
            if (handle != 0)
            {
                NativeMethods.DeleteProcThreadAttributeList(handle);
                Marshal.FreeHGlobal(handle);
            }

            foreach (nint value in _values)
            {
                Marshal.FreeHGlobal(value);
            }

            _values.Clear();
        }
    }

    private sealed partial class WindowsJobScope : IDisposable
    {
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
                CloseHandle(ref _handle);
                throw NativeFailure("configure the Checkpoint B process job", error);
            }
        }

        internal nint Handle => _handle != 0
            ? _handle
            : throw new ObjectDisposedException(nameof(WindowsJobScope));

        public void Dispose()
        {
            CloseHandle(ref _handle);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CreateProcess(
            string? applicationName,
            nint commandLine,
            nint processAttributes,
            nint threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            nint environment,
            string? currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CreatePipe(
            out nint readPipe,
            out nint writePipe,
            ref SecurityAttributes pipeAttributes,
            uint size);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetHandleInformation(nint handle, uint mask, uint flags);

        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial nint CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SecurityAttributes securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            nint templateFile);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool InitializeProcThreadAttributeList(
            nint attributeList,
            int attributeCount,
            uint flags,
            ref nuint size);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UpdateProcThreadAttribute(
            nint attributeList,
            uint flags,
            nuint attribute,
            nint value,
            nuint size,
            nint previousValue,
            nint returnSize);

        [LibraryImport("kernel32.dll")]
        internal static partial void DeleteProcThreadAttributeList(nint attributeList);

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
        internal static partial uint ResumeThread(nint thread);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool TerminateProcess(nint process, uint exitCode);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial uint WaitForSingleObject(nint handle, uint milliseconds);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(nint handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal uint Size;
        internal nint SecurityDescriptor;
        internal int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        internal uint Size;
        internal nint Reserved;
        internal nint Desktop;
        internal nint Title;
        internal uint X;
        internal uint Y;
        internal uint XSize;
        internal uint YSize;
        internal uint XCountChars;
        internal uint YCountChars;
        internal uint FillAttribute;
        internal uint Flags;
        internal ushort ShowWindow;
        internal ushort ReservedByteCount;
        internal nint ReservedBytes;
        internal nint StandardInput;
        internal nint StandardOutput;
        internal nint StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal nint Process;
        internal nint Thread;
        internal uint ProcessId;
        internal uint ThreadId;
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
