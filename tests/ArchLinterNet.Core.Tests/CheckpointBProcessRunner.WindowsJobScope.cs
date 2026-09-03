using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.Tests;

internal static partial class CheckpointBProcessRunner
{
    private sealed partial class WindowsJobScope : IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectExtendedLimitInformationClass = 9;

        private const uint StartfUseStdHandles = 0x00000100;
        private const uint ExtendedStartupInfoPresent = 0x00080000;
        private const uint CreateUnicodeEnvironment = 0x00000400;
        private const uint CreateNoWindowFlag = 0x08000000;
        private const uint CreateSuspendedFlag = 0x00000004;
        private const uint HandleFlagInherit = 0x00000001;
        private const uint InvalidResumeCount = 0xFFFFFFFF;
        private const nuint ProcThreadAttributeJobList = 0x2000D;
        private const nuint ProcThreadAttributeHandleList = 0x20002;
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x00000080;
        private static readonly nint _invalidHandleValue = -1;
        private static readonly char[] _pathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

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

        /// <summary>
        /// Starts <paramref name="startInfo"/> with this job attached via
        /// PROC_THREAD_ATTRIBUTE_JOB_LIST, so containment happens atomically at process creation
        /// rather than through a separate post-start AssignProcessToJobObject call. Inheritance is
        /// further scoped with PROC_THREAD_ATTRIBUTE_HANDLE_LIST to exactly the three redirected
        /// stdio handles, so the child cannot pick up unrelated inheritable handles that happen to
        /// be open in the test host process. The process is created CREATE_SUSPENDED and only
        /// resumed after a managed <see cref="Process"/> has been attached by PID, so the root
        /// cannot exit (and free its PID for reuse) before that attachment is guaranteed to name
        /// the process this method just created; CreateProcessW's own hProcess is held open as the
        /// authoritative reference for that entire window and used to force-terminate the child if
        /// attachment fails.
        /// </summary>
        internal (Process Process, StreamReader StandardOutput, StreamReader StandardError) LaunchContained(
            ProcessStartInfo startInfo)
        {
            SafeFileHandle? childStandardOutput = null;
            SafeFileHandle? childStandardError = null;
            SafeFileHandle? parentStandardOutput = null;
            SafeFileHandle? parentStandardError = null;
            SafeFileHandle? standardInput = null;
            nint attributeList = 0;
            nint jobListBuffer = 0;
            nint handleListBuffer = 0;
            nint environmentBlock = 0;
            nint commandLineBuffer = 0;
            nint rawProcessHandle = 0;
            nint rawThreadHandle = 0;
            try
            {
                CreateInheritablePipe(out parentStandardOutput, out childStandardOutput);
                CreateInheritablePipe(out parentStandardError, out childStandardError);
                standardInput = CreateInheritableNullHandle();

                nint[] inheritableHandles =
                [
                    standardInput.DangerousGetHandle(),
                    childStandardOutput.DangerousGetHandle(),
                    childStandardError.DangerousGetHandle(),
                ];
                attributeList = CreateProcessAttributeList(
                    _handle,
                    inheritableHandles,
                    out jobListBuffer,
                    out handleListBuffer);
                environmentBlock = BuildEnvironmentBlock(startInfo.Environment);
                commandLineBuffer = Marshal.StringToHGlobalUni(BuildCommandLine(startInfo));
                string executablePath = ResolveExecutablePath(startInfo.FileName);

                var startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
                startupInfo.StartupInfo.Flags = StartfUseStdHandles;
                startupInfo.StartupInfo.StandardInput = standardInput.DangerousGetHandle();
                startupInfo.StartupInfo.StandardOutput = childStandardOutput.DangerousGetHandle();
                startupInfo.StartupInfo.StandardError = childStandardError.DangerousGetHandle();
                startupInfo.AttributeList = attributeList;

                uint creationFlags = ExtendedStartupInfoPresent | CreateUnicodeEnvironment | CreateSuspendedFlag;
                if (startInfo.CreateNoWindow)
                {
                    creationFlags |= CreateNoWindowFlag;
                }

                bool created = NativeMethods.CreateProcess(
                    executablePath,
                    commandLineBuffer,
                    0,
                    0,
                    inheritHandles: true,
                    creationFlags,
                    environmentBlock,
                    string.IsNullOrEmpty(startInfo.WorkingDirectory) ? null : startInfo.WorkingDirectory,
                    ref startupInfo,
                    out ProcessInformation processInformation);
                if (!created)
                {
                    throw NativeFailure("create the Checkpoint B contained process");
                }

                rawProcessHandle = processInformation.Process;
                rawThreadHandle = processInformation.Thread;

                // The primary thread is still suspended, so the process cannot exit and free its
                // PID for reuse: GetProcessById is guaranteed to attach to the process this method
                // just created, not to an unrelated process that happens to reuse the same PID.
                Process process;
                try
                {
                    process = Process.GetProcessById(unchecked((int)processInformation.ProcessId));
                }
                catch
                {
                    NativeMethods.TerminateProcess(rawProcessHandle, InvalidResumeCount);
                    throw;
                }

                uint previousSuspendCount = NativeMethods.ResumeThread(rawThreadHandle);
                if (previousSuspendCount == InvalidResumeCount)
                {
                    int error = Marshal.GetLastWin32Error();
                    NativeMethods.TerminateProcess(rawProcessHandle, InvalidResumeCount);
                    process.Dispose();
                    throw NativeFailure("resume the Checkpoint B contained process", error);
                }

                Encoding outputEncoding = startInfo.StandardOutputEncoding ?? Console.OutputEncoding;
                Encoding errorEncoding = startInfo.StandardErrorEncoding ?? Console.OutputEncoding;
                var standardOutputReader = new StreamReader(new FileStream(parentStandardOutput, FileAccess.Read), outputEncoding);
                parentStandardOutput = null;
                var standardErrorReader = new StreamReader(new FileStream(parentStandardError, FileAccess.Read), errorEncoding);
                parentStandardError = null;
                return (process, standardOutputReader, standardErrorReader);
            }
            finally
            {
                if (rawThreadHandle != 0)
                {
                    NativeMethods.CloseHandle(rawThreadHandle);
                }

                if (rawProcessHandle != 0)
                {
                    NativeMethods.CloseHandle(rawProcessHandle);
                }

                childStandardOutput?.Dispose();
                childStandardError?.Dispose();
                parentStandardOutput?.Dispose();
                parentStandardError?.Dispose();
                standardInput?.Dispose();
                if (attributeList != 0)
                {
                    NativeMethods.DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }

                if (jobListBuffer != 0)
                {
                    Marshal.FreeHGlobal(jobListBuffer);
                }

                if (handleListBuffer != 0)
                {
                    Marshal.FreeHGlobal(handleListBuffer);
                }

                if (environmentBlock != 0)
                {
                    Marshal.FreeHGlobal(environmentBlock);
                }

                if (commandLineBuffer != 0)
                {
                    Marshal.FreeHGlobal(commandLineBuffer);
                }
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

        private static void CreateInheritablePipe(out SafeFileHandle parentRead, out SafeFileHandle childWrite)
        {
            var attributes = new SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = 0,
                InheritHandle = 1,
            };

            if (!NativeMethods.CreatePipe(out nint readHandle, out nint writeHandle, ref attributes, 0))
            {
                throw NativeFailure("create a Checkpoint B redirected stream pipe");
            }

            parentRead = new SafeFileHandle(readHandle, ownsHandle: true);
            childWrite = new SafeFileHandle(writeHandle, ownsHandle: true);

            if (!NativeMethods.SetHandleInformation(parentRead.DangerousGetHandle(), HandleFlagInherit, 0))
            {
                throw NativeFailure("mark a Checkpoint B pipe read handle non-inheritable");
            }
        }

        private static nint CreateProcessAttributeList(
            nint jobHandle,
            nint[] inheritableHandles,
            out nint jobListBuffer,
            out nint handleListBuffer)
        {
            jobListBuffer = 0;
            handleListBuffer = 0;
            const int AttributeCount = 2;
            nuint size = 0;
            _ = NativeMethods.InitializeProcThreadAttributeList(0, AttributeCount, 0, ref size);
            nint attributeList = Marshal.AllocHGlobal((nint)size);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, AttributeCount, 0, ref size))
            {
                Marshal.FreeHGlobal(attributeList);
                throw NativeFailure("initialize the Checkpoint B process attribute list");
            }

            jobListBuffer = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(jobListBuffer, jobHandle);
            if (!NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeJobList,
                    jobListBuffer,
                    (nuint)nint.Size,
                    0,
                    0))
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                Marshal.FreeHGlobal(jobListBuffer);
                jobListBuffer = 0;
                throw NativeFailure("attach the Checkpoint B job list to process creation", error);
            }

            // Restrict inheritance to exactly these handles; otherwise CreateProcess(inheritHandles:
            // true) would pass through every inheritable handle currently open in the test host.
            handleListBuffer = Marshal.AllocHGlobal(nint.Size * inheritableHandles.Length);
            for (int index = 0; index < inheritableHandles.Length; index++)
            {
                Marshal.WriteIntPtr(handleListBuffer, index * nint.Size, inheritableHandles[index]);
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    handleListBuffer,
                    (nuint)(nint.Size * inheritableHandles.Length),
                    0,
                    0))
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                Marshal.FreeHGlobal(jobListBuffer);
                Marshal.FreeHGlobal(handleListBuffer);
                jobListBuffer = 0;
                handleListBuffer = 0;
                throw NativeFailure("attach the Checkpoint B handle list to process creation", error);
            }

            return attributeList;
        }

        private static SafeFileHandle CreateInheritableNullHandle()
        {
            var attributes = new SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = 0,
                InheritHandle = 1,
            };

            nint handle = NativeMethods.CreateFile(
                "NUL",
                GenericRead,
                FileShareRead | FileShareWrite,
                ref attributes,
                OpenExisting,
                FileAttributeNormal,
                0);
            if (handle == _invalidHandleValue)
            {
                throw NativeFailure("open the Checkpoint B null stdin handle");
            }

            return new SafeFileHandle(handle, ownsHandle: true);
        }

        private static nint BuildEnvironmentBlock(IDictionary<string, string?> environment)
        {
            var block = new StringBuilder();
            foreach (string key in environment.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase))
            {
                block.Append(key).Append('=').Append(environment[key] ?? string.Empty).Append('\0');
            }

            block.Append('\0');
            return Marshal.StringToHGlobalUni(block.ToString());
        }

        private static string ResolveExecutablePath(string fileName)
        {
            if (fileName.IndexOfAny(_pathSeparators) >= 0)
            {
                string fullPath = Path.GetFullPath(fileName);
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"Could not find the executable '{fileName}'.");
                }

                return fullPath;
            }

            IEnumerable<string> extensions = Path.HasExtension(fileName) ? [string.Empty] : PathExtensions;
            IEnumerable<string> directories = [Environment.CurrentDirectory, .. PathDirectories];
            foreach (string directory in directories)
            {
                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(directory, fileName + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException($"Could not resolve executable '{fileName}' on PATH.");
        }

        private static IEnumerable<string> PathExtensions =>
            (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Prepend(string.Empty);

        private static IEnumerable<string> PathDirectories =>
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
            internal static partial bool CloseHandle(nint handle);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool CreatePipe(
                out nint readHandle,
                out nint writeHandle,
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
                int flags,
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
            internal static partial uint ResumeThread(nint thread);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool TerminateProcess(nint process, uint exitCode);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            internal uint Length;
            internal nint SecurityDescriptor;
            internal int InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            internal int Size;
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
            internal ushort Reserved2Size;
            internal nint Reserved2;
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
}
