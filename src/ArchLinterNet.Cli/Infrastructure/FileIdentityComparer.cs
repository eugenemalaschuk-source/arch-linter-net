using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Cli.Infrastructure;

/// <summary>Compares existing files by operating-system identity, following symbolic links.</summary>
internal static partial class FileIdentityComparer
{
    private const int StatBufferSize = 512;

    internal static bool TryAreDifferentFiles(string firstPath, string secondPath)
    {
        return TryGetIdentity(firstPath, out FileIdentity first)
            && TryGetIdentity(secondPath, out FileIdentity second)
            && first != second;
    }

    private static bool TryGetIdentity(string path, out FileIdentity identity)
    {
        try
        {
            return OperatingSystem.IsWindows()
                ? TryGetWindowsIdentity(path, out identity)
                : TryGetUnixIdentity(path, out identity);
        }
        catch (IOException)
        {
            identity = default;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            identity = default;
            return false;
        }
    }

    private static bool TryGetWindowsIdentity(string path, out FileIdentity identity)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out ByHandleFileInformation information))
        {
            identity = default;
            return false;
        }

        identity = new FileIdentity(
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
        return true;
    }

    private static bool TryGetUnixIdentity(string path, out FileIdentity identity)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        IntPtr buffer = Marshal.AllocHGlobal(StatBufferSize);
        try
        {
            int result = OperatingSystem.IsMacOS()
                ? FStatMacOsInode64(stream.SafeFileHandle, buffer)
                : FStatUnix(stream.SafeFileHandle, buffer);
            if (result != 0)
            {
                identity = default;
                return false;
            }

            // The modern Darwin fstat$INODE64 and Linux fstat ABIs both place the device at byte 0
            // and the 64-bit inode at byte 8. FileStream follows a symlink before reading the
            // opened file descriptor.
            ulong device = OperatingSystem.IsMacOS()
                ? unchecked((uint)Marshal.ReadInt32(buffer, 0))
                : unchecked((ulong)Marshal.ReadInt64(buffer, 0));
            ulong inode = unchecked((ulong)Marshal.ReadInt64(buffer, 8));
            identity = new FileIdentity(device, inode);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute",
        Justification = "The Windows output struct embeds ComTypes.FILETIME fields; LibraryImport source generation cannot marshal this type without assembly-wide DisableRuntimeMarshalling.")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [LibraryImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static partial int FStatUnix(SafeFileHandle fileDescriptor, IntPtr buffer);

    // macOS's unadorned fstat symbol uses the legacy 32-bit inode layout, which cannot be read
    // using the current struct stat offsets above. Pin the modern ABI explicitly.
    [LibraryImport("libc", SetLastError = true, EntryPoint = "fstat$INODE64")]
    private static partial int FStatMacOsInode64(SafeFileHandle fileDescriptor, IntPtr buffer);

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

    private readonly record struct FileIdentity(ulong Device, ulong Inode);
}
