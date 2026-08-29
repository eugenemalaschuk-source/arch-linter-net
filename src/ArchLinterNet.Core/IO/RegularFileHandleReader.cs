using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.IO;

// Owns the native handle checks shared by callers that must consume only real regular files.
// Opening a Unix descriptor in non-blocking mode prevents a FIFO substituted during a path race
// from indefinitely blocking the caller before fstat can reject it.
internal static partial class RegularFileHandleReader
{
    internal static FileStream Open(string path)
    {
        return OperatingSystem.IsWindows() ? OpenWindows(path) : OpenUnix(path);
    }

    internal static string GetIdentity(Stream stream)
    {
        if (stream is not FileStream fileStream)
        {
            throw NotRegular("Binary evidence must be opened through a regular file handle.");
        }

        return GetIdentity(fileStream.SafeFileHandle);
    }

    private static FileStream OpenWindows(string path)
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
            _ = GetIdentity(handle);
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static FileStream OpenUnix(string path)
    {
        int descriptor = OpenUnixDescriptor(path, OpenReadOnly | OpenNonBlocking | OpenNoFollow);
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

    private static string GetIdentity(SafeFileHandle handle)
    {
        if (handle.IsInvalid)
        {
            throw Unreadable("The opened evidence handle is invalid.");
        }

        return OperatingSystem.IsWindows()
            ? GetWindowsIdentity(handle)
            : GetUnixIdentity(handle);
    }

    private static string GetWindowsIdentity(SafeFileHandle handle)
    {
        uint fileType = GetFileType(handle);
        int fileTypeError = fileType == FileTypeUnknown ? Marshal.GetLastPInvokeError() : 0;
        if (fileTypeError != 0)
        {
            throw ClassifyWindowsFailure(fileTypeError);
        }

        if (fileType != FileTypeDisk)
        {
            throw NotRegular("External evidence must be a regular file.");
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw ClassifyWindowsFailure(Marshal.GetLastPInvokeError());
        }

        FileAttributes attributes = File.GetAttributes(handle);
        if ((information.FileAttributes & FileAttributeDirectory) != 0
            || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw NotRegular("External evidence must not be a directory or reparse point.");
        }

        ulong index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return $"windows:{information.VolumeSerialNumber:X8}:{index:X16}";
    }

    private static string GetUnixIdentity(SafeFileHandle handle)
    {
        int descriptor = checked((int)handle.DangerousGetHandle());
        if (OperatingSystem.IsMacOS())
        {
            return GetMacOsIdentity(descriptor);
        }

        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => GetLinuxX64Identity(descriptor),
            Architecture.Arm64 => GetLinuxArm64Identity(descriptor),
            _ => throw NotRegular("The current Unix architecture is not supported for regular-file verification."),
        };
    }

    private static string GetLinuxX64Identity(int descriptor)
    {
        if (FStatLinuxX64(descriptor, out LinuxX64Stat stat) != 0)
        {
            throw ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        return CreateUnixIdentity(stat.Device, stat.Inode, stat.Mode);
    }

    private static string GetLinuxArm64Identity(int descriptor)
    {
        if (FStatLinuxArm64(descriptor, out LinuxArm64Stat stat) != 0)
        {
            throw ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        return CreateUnixIdentity(stat.Device, stat.Inode, stat.Mode);
    }

    private static string CreateUnixIdentity(ulong device, ulong inode, uint mode)
    {
        if ((mode & FileTypeMask) != RegularFile)
        {
            throw NotRegular("External evidence must be a regular file.");
        }

        return $"unix:{device:X16}:{inode:X16}";
    }

    private static string GetMacOsIdentity(int descriptor)
    {
        var attributes = new DarwinAttributeList
        {
            BitmapCount = AttributeBitMapCount,
            CommonAttributes = CommonDeviceAttribute | CommonObjectTypeAttribute | CommonFileIdAttribute,
        };
        if (FGetAttributeList(
                descriptor,
                ref attributes,
                out DarwinFileIdentityAttributes identity,
                (nuint)Marshal.SizeOf<DarwinFileIdentityAttributes>(),
                options: 0) != 0)
        {
            throw ClassifyUnixFailure(Marshal.GetLastPInvokeError());
        }

        if (identity.ObjectType != DarwinRegularFile)
        {
            throw NotRegular("External evidence must be a regular file.");
        }

        return $"darwin:{identity.Device:X8}:{identity.FileId:X16}";
    }

    private static IOException ClassifyWindowsFailure(int error)
    {
        return error switch
        {
            2 or 3 => Missing("The external evidence file does not exist."),
            5 or 32 => Unreadable("The external evidence file cannot be opened."),
            _ => Unreadable($"The external evidence file could not be inspected (Win32 {error})."),
        };
    }

    private static Exception ClassifyUnixFailure(int error)
    {
        if (error == UnixSymbolicLinkLoopError)
        {
            return NotRegular("External evidence must not cross a symbolic-link or reparse-point boundary.");
        }

        return error switch
        {
            2 or 20 => Missing("The external evidence file does not exist."),
            1 or 13 => Unreadable("The external evidence file cannot be opened."),
            _ => Unreadable($"The external evidence file could not be inspected (errno {error})."),
        };
    }

    private static FileNotFoundException Missing(string message) => new(message);

    private static InvalidDataException NotRegular(string message) => new(message);

    private static IOException Unreadable(string message) => new(message);

    private const uint GenericRead = 0x80000000;
    private const int OpenReadOnly = 0;
    private static int OpenNonBlocking => OperatingSystem.IsMacOS() ? 0x0004 : 0x0800;
    private static int OpenNoFollow => OperatingSystem.IsMacOS() ? 0x0100 : 0x20000;
    private static int UnixSymbolicLinkLoopError => OperatingSystem.IsMacOS() ? 62 : 40;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileTypeUnknown = 0x00000000;
    private const uint FileTypeDisk = 0x00000001;
    private const uint FileTypeMask = 0xF000;
    private const uint RegularFile = 0x8000;
    private const ushort AttributeBitMapCount = 5;
    private const uint CommonDeviceAttribute = 0x00000002;
    private const uint CommonObjectTypeAttribute = 0x00000008;
    private const uint CommonFileIdAttribute = 0x02000000;
    private const uint DarwinRegularFile = 1;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "ByHandleFileInformation embeds ComTypes.FILETIME, unsupported by LibraryImport without assembly-wide DisableRuntimeMarshalling.")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetFileType(SafeFileHandle file);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "stat uses an ABI-specific stat buffer unsupported by LibraryImport.")]
    [DllImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static extern int FStatLinuxX64(int descriptor, out LinuxX64Stat stat);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "stat uses an ABI-specific stat buffer unsupported by LibraryImport.")]
    [DllImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static extern int FStatLinuxArm64(int descriptor, out LinuxArm64Stat stat);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenUnixDescriptor(string path, int flags);

    [LibraryImport("libc", EntryPoint = "fgetattrlist", SetLastError = true)]
    private static partial int FGetAttributeList(
        int descriptor,
        ref DarwinAttributeList attributes,
        out DarwinFileIdentityAttributes attributeBuffer,
        nuint attributeBufferSize,
        uint options);

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxX64Stat
    {
        public ulong Device;
        public ulong Inode;
        public ulong LinkCount;
        public uint Mode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] RemainingFields;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxArm64Stat
    {
        public ulong Device;
        public ulong Inode;
        public uint Mode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] RemainingFields;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DarwinAttributeList
    {
        public ushort BitmapCount;
        public ushort Reserved;
        public uint CommonAttributes;
        public uint VolumeAttributes;
        public uint DirectoryAttributes;
        public uint FileAttributes;
        public uint ForkAttributes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct DarwinFileIdentityAttributes
    {
        public uint Length;
        public uint Device;
        public uint ObjectType;
        public ulong FileId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
