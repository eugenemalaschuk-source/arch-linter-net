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

        if (OperatingSystem.IsMacOS())
        {
            DarwinStat stat = default;
            int result = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => FStatMacOs(stream.SafeFileHandle, out stat),
                Architecture.X64 => FStatMacOsInode64(stream.SafeFileHandle, out stat),
                _ => -1
            };
            if (result != 0)
            {
                identity = default;
                return false;
            }

            identity = new FileIdentity(unchecked((uint)stat.Device), stat.Inode);
            return true;
        }

        IntPtr buffer = Marshal.AllocHGlobal(StatBufferSize);
        try
        {
            int result = FStatUnix(stream.SafeFileHandle, buffer);
            if (result != 0)
            {
                identity = default;
                return false;
            }

            // Linux's 64-bit struct stat places the device at byte 0 and inode at byte 8.
            // FileStream follows a symlink before reading the opened file descriptor.
            ulong device = unchecked((ulong)Marshal.ReadInt64(buffer, 0));
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

    // The macOS SDK declares fstat as fstat$INODE64 on x86_64, while arm64 exports the 64-bit
    // struct stat ABI as fstat. Darwin's 64-bit struct stat is 144 bytes with st_dev at offset 0
    // and st_ino at offset 8 on both architectures.
    [LibraryImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static partial int FStatMacOs(SafeFileHandle fileDescriptor, out DarwinStat stat);

    [LibraryImport("libc", SetLastError = true, EntryPoint = "fstat$INODE64")]
    private static partial int FStatMacOsInode64(SafeFileHandle fileDescriptor, out DarwinStat stat);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)]
        public int Device;

        [FieldOffset(8)]
        public ulong Inode;
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

    private readonly record struct FileIdentity(ulong Device, ulong Inode);
}
