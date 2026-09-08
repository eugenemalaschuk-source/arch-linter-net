using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.IO;

internal static class RepositoryLocalRegularFileReader
{
    internal static RepositoryRoot OpenRepositoryRoot(string repositoryRoot)
    {
        return OperatingSystem.IsWindows()
            ? new RepositoryRoot(OpenWindowsDirectory(repositoryRoot), isWindows: true)
            : new RepositoryRoot(OpenUnixDirectory(repositoryRoot), isWindows: false);
    }

    internal static FileStream OpenRepositoryLocal(RepositoryRoot repositoryRoot, string relativePath)
    {
        string[] segments = SplitRelativePath(relativePath);
        return repositoryRoot.IsWindows
            ? OpenWindowsRepositoryLocal(repositoryRoot.Handle, segments)
            : OpenUnixRepositoryLocal(repositoryRoot.Handle, segments);
    }

    private static string[] SplitRelativePath(string relativePath)
    {
        string[] segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
        {
            throw RegularFileHandleReader.NotRegular("External evidence must be a non-empty repository-relative path without alternate data streams.");
        }

        return segments;
    }

    private static FileStream OpenUnixRepositoryLocal(SafeFileHandle rootHandle, IReadOnlyList<string> segments)
    {
        return OpenUnixDescendant(rootHandle, segments, 0);
    }

    private static FileStream OpenUnixDescendant(SafeFileHandle directoryHandle, IReadOnlyList<string> segments, int index)
    {
        if (index == segments.Count - 1)
        {
            return OpenUnixRegularFileAt(directoryHandle, segments[index]);
        }

        using SafeFileHandle childDirectory = OpenUnixDirectoryAt(directoryHandle, segments[index]);
        return OpenUnixDescendant(childDirectory, segments, index + 1);
    }

    private static SafeFileHandle OpenUnixDirectory(string path)
    {
        int descriptor = OpenUnixDescriptor(path, OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenDirectory);
        return CreateUnixDirectoryHandle(descriptor);
    }

    private static SafeFileHandle OpenUnixDirectoryAt(SafeFileHandle directoryHandle, string segment)
    {
        int descriptor = OpenUnixDescriptorAt(
            directoryHandle,
            segment,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenDirectory);
        return CreateUnixDirectoryHandle(descriptor);
    }

    private static SafeFileHandle CreateUnixDirectoryHandle(int descriptor)
    {
        if (descriptor < 0)
        {
            throw RegularFileHandleReader.ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
    }

    private static FileStream OpenUnixRegularFileAt(SafeFileHandle directoryHandle, string segment)
    {
        int descriptor = OpenUnixDescriptorAt(
            directoryHandle,
            segment,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow);
        if (descriptor < 0)
        {
            throw RegularFileHandleReader.ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        try
        {
            _ = RegularFileHandleReader.GetIdentity(handle);
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [ExcludeFromCodeCoverage]
    private static FileStream OpenWindowsRepositoryLocal(SafeFileHandle rootHandle, IReadOnlyList<string> segments)
    {
        return OpenWindowsDescendant(rootHandle, segments, 0);
    }

    [ExcludeFromCodeCoverage]
    private static FileStream OpenWindowsDescendant(SafeFileHandle directoryHandle, IReadOnlyList<string> segments, int index)
    {
        if (index == segments.Count - 1)
        {
            SafeFileHandle fileHandle = OpenWindowsRelative(directoryHandle, segments[index], directory: false);
            try
            {
                _ = RegularFileHandleReader.GetIdentity(fileHandle);
                return new FileStream(fileHandle, FileAccess.Read);
            }
            catch
            {
                fileHandle.Dispose();
                throw;
            }
        }

        using SafeFileHandle childDirectory = OpenWindowsRelative(directoryHandle, segments[index], directory: true);
        return OpenWindowsDescendant(childDirectory, segments, index + 1);
    }

    [ExcludeFromCodeCoverage]
    private static SafeFileHandle OpenWindowsDirectory(string path)
    {
        SafeFileHandle handle = CreateFile(
            path,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal | FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw RegularFileHandleReader.ClassifyWindowsFailure(error);
        }

        try
        {
            RegularFileHandleReader.EnsureWindowsDirectory(handle);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [ExcludeFromCodeCoverage]
    private static SafeFileHandle OpenWindowsRelative(SafeFileHandle directoryHandle, string segment, bool directory)
    {
        IntPtr buffer = Marshal.StringToHGlobalUni(segment);
        IntPtr unicodeStringPointer = IntPtr.Zero;
        bool rootHandleReference = false;
        try
        {
            directoryHandle.DangerousAddRef(ref rootHandleReference);
            var unicodeString = new UnicodeString
            {
                Length = checked((ushort)(segment.Length * sizeof(char))),
                MaximumLength = checked((ushort)((segment.Length + 1) * sizeof(char))),
                Buffer = buffer,
            };
            unicodeStringPointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicodeString, unicodeStringPointer, fDeleteOld: false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = directoryHandle.DangerousGetHandle(), // NOSONAR: the reference above pins the native root handle for NtCreateFile.
                ObjectName = unicodeStringPointer,
                Attributes = ObjectCaseInsensitive,
            };
            int status = NtCreateFile(
                out SafeFileHandle handle,
                GenericRead | Synchronize,
                ref attributes,
                out _,
                IntPtr.Zero,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                FileOpen,
                FileSynchronousIoNonAlert | FileOpenReparsePoint | (directory ? FileDirectoryFile : FileNonDirectoryFile),
                IntPtr.Zero,
                0);
            if (status < 0)
            {
                handle.Dispose();
                throw RegularFileHandleReader.ClassifyNtStatus(status);
            }

            try
            {
                if (directory)
                {
                    RegularFileHandleReader.EnsureWindowsDirectory(handle);
                }

                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            if (rootHandleReference)
            {
                directoryHandle.DangerousRelease();
            }

            if (unicodeStringPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodeStringPointer);
            }

            Marshal.FreeHGlobal(buffer);
        }
    }

    private const uint GenericRead = 0x80000000;
    private const int OpenReadOnly = 0;
    private static int OpenNonBlocking => OperatingSystem.IsMacOS() ? 0x0004 : 0x0800;
    private static int OpenNoFollow => OperatingSystem.IsMacOS() ? 0x0100 : 0x20000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint Synchronize = 0x00100000;
    private const uint FileOpen = 0x00000001;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const int OpenDirectoryLinux = 0x10000;
    private const int OpenDirectoryMacOs = 0x100000;
    private static int OpenDirectory => OperatingSystem.IsMacOS() ? OpenDirectoryMacOs : OpenDirectoryLinux;

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The collaborator is intentionally non-partial; explicit UTF-8 marshalling preserves the open/openat ABI.")]
    [DllImport("libc", EntryPoint = "open", ExactSpelling = true, SetLastError = true)]
    private static extern int OpenUnixDescriptor(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The collaborator is intentionally non-partial; explicit UTF-8 marshalling preserves the openat ABI.")]
    [DllImport("libc", EntryPoint = "openat", ExactSpelling = true, SetLastError = true)]
    private static extern int OpenUnixDescriptorAt(
        SafeFileHandle directoryHandle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The collaborator is intentionally non-partial and uses the Windows UTF-16 entry point.")]
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "NtCreateFile uses native pointer-backed object attributes.")]
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr extendedAttributes,
        uint extendedAttributesLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    internal sealed class RepositoryRoot(SafeFileHandle handle, bool isWindows) : IDisposable
    {
        internal SafeFileHandle Handle { get; } = handle;

        internal bool IsWindows { get; } = isWindows;

        public void Dispose()
        {
            Handle.Dispose();
        }
    }
}
