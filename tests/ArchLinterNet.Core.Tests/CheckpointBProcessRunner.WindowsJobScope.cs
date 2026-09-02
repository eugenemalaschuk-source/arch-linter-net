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
        private const uint HandleFlagInherit = 0x00000001;
        private const nuint ProcThreadAttributeJobList = 0x2000D;
        private const int StdInputHandle = -10;
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
        /// rather than through a separate post-start AssignProcessToJobObject call.
        /// </summary>
        internal (Process Process, StreamReader StandardOutput, StreamReader StandardError) LaunchContained(
            ProcessStartInfo startInfo)
        {
            SafeFileHandle? childStandardOutput = null;
            SafeFileHandle? childStandardError = null;
            SafeFileHandle? parentStandardOutput = null;
            SafeFileHandle? parentStandardError = null;
            nint attributeList = 0;
            nint jobListBuffer = 0;
            nint environmentBlock = 0;
            nint commandLineBuffer = 0;
            try
            {
                CreateInheritablePipe(out parentStandardOutput, out childStandardOutput);
                CreateInheritablePipe(out parentStandardError, out childStandardError);

                attributeList = CreateJobListAttributeList(_handle, out jobListBuffer);
                environmentBlock = BuildEnvironmentBlock(startInfo.Environment);
                commandLineBuffer = Marshal.StringToHGlobalUni(BuildCommandLine(startInfo));
                string executablePath = ResolveExecutablePath(startInfo.FileName);

                nint stdInputHandle = NativeMethods.GetStdHandle(StdInputHandle);
                var startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
                startupInfo.StartupInfo.Flags = StartfUseStdHandles;
                startupInfo.StartupInfo.StandardInput = stdInputHandle == _invalidHandleValue ? 0 : stdInputHandle;
                startupInfo.StartupInfo.StandardOutput = childStandardOutput.DangerousGetHandle();
                startupInfo.StartupInfo.StandardError = childStandardError.DangerousGetHandle();
                startupInfo.AttributeList = attributeList;

                uint creationFlags = ExtendedStartupInfoPresent | CreateUnicodeEnvironment;
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

                Process process = Process.GetProcessById(unchecked((int)processInformation.ProcessId));
                NativeMethods.CloseHandle(processInformation.Thread);
                NativeMethods.CloseHandle(processInformation.Process);

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
                childStandardOutput?.Dispose();
                childStandardError?.Dispose();
                parentStandardOutput?.Dispose();
                parentStandardError?.Dispose();
                if (attributeList != 0)
                {
                    NativeMethods.DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }

                if (jobListBuffer != 0)
                {
                    Marshal.FreeHGlobal(jobListBuffer);
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

        private static nint CreateJobListAttributeList(nint jobHandle, out nint jobListBuffer)
        {
            jobListBuffer = 0;
            nuint size = 0;
            _ = NativeMethods.InitializeProcThreadAttributeList(0, 1, 0, ref size);
            nint attributeList = Marshal.AllocHGlobal((nint)size);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
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

            return attributeList;
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

            [LibraryImport("kernel32.dll", SetLastError = true)]
            internal static partial nint GetStdHandle(int stdHandle);

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
