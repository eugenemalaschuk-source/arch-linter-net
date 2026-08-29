using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.IO;

internal static partial class RegularFileHandleReader
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
            throw NotRegular("External evidence must be a non-empty repository-relative path without alternate data streams.");
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
            checked((int)directoryHandle.DangerousGetHandle()),
            segment,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenDirectory);
        return CreateUnixDirectoryHandle(descriptor);
    }

    private static SafeFileHandle CreateUnixDirectoryHandle(int descriptor)
    {
        if (descriptor < 0)
        {
            throw ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
    }

    private static FileStream OpenUnixRegularFileAt(SafeFileHandle directoryHandle, string segment)
    {
        int descriptor = OpenUnixDescriptorAt(
            checked((int)directoryHandle.DangerousGetHandle()),
            segment,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow);
        if (descriptor < 0)
        {
            throw ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        try
        {
            _ = GetIdentity(handle);
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
                _ = GetIdentity(fileHandle);
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
            throw ClassifyWindowsFailure(error);
        }

        try
        {
            EnsureWindowsDirectory(handle);
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
        try
        {
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
                RootDirectory = directoryHandle.DangerousGetHandle(),
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
                throw ClassifyNtStatus(status);
            }

            try
            {
                if (directory)
                {
                    EnsureWindowsDirectory(handle);
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
            if (unicodeStringPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodeStringPointer);
            }

            Marshal.FreeHGlobal(buffer);
        }
    }

    [ExcludeFromCodeCoverage]
    private static void EnsureWindowsDirectory(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw ClassifyWindowsFailure(Marshal.GetLastPInvokeError());
        }

        FileAttributes attributes = File.GetAttributes(handle);
        if ((information.FileAttributes & FileAttributeDirectory) == 0
            || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
        {
            throw NotRegular("External evidence must not cross a directory reparse point.");
        }
    }

    [ExcludeFromCodeCoverage]
    private static Exception ClassifyNtStatus(int status)
    {
        return unchecked((uint)status) switch
        {
            0xC000000F or 0xC0000034 or 0xC000003A => Missing("The external evidence file does not exist."),
            0xC00000BA or 0xC000050B or 0x8000002D => NotRegular("External evidence must not cross a symbolic-link or reparse-point boundary."),
            _ => Unreadable($"The external evidence file could not be inspected (NTSTATUS 0x{unchecked((uint)status):X8})."),
        };
    }

    private const int OpenDirectoryLinux = 0x10000;
    private const int OpenDirectoryMacOs = 0x100000;
    private static int OpenDirectory => OperatingSystem.IsMacOS() ? OpenDirectoryMacOs : OpenDirectoryLinux;
    private const uint Synchronize = 0x00100000;
    private const uint FileOpen = 0x00000001;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjectCaseInsensitive = 0x00000040;

    [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenUnixDescriptorAt(int directoryDescriptor, string path, int flags);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "NtCreateFile uses native pointer-backed object attributes.")]
    [DllImport("ntdll.dll")]
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
