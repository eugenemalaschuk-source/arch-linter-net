using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal sealed class ArchitecturePolicyPathResolver : IArchitecturePolicyPathResolver
{
    public ArchitecturePolicyRootPath ResolveRoot(string rootPath)
    {
        string fullPath = Path.GetFullPath(rootPath);
        string policyDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.OutOfBoundary,
                $"Cannot determine the policy directory for '{rootPath}'.");
        string boundary = string.Equals(
            Path.GetFileName(policyDirectory),
            "architecture",
            StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(policyDirectory) ?? policyDirectory
            : policyDirectory;
        string exactRoot = ResolveExactPath(boundary, fullPath, rootPath);
        string physicalBoundary = ResolvePhysicalPath(boundary, boundary);
        string physicalRoot = ResolvePhysicalPath(boundary, exactRoot);
        EnsureWithinBoundary(physicalBoundary, physicalRoot, rootPath);
        string fileIdentity = GetRegularFileIdentity(physicalRoot, rootPath);

        return new ArchitecturePolicyRootPath(rootPath, exactRoot, physicalRoot, boundary, physicalBoundary, fileIdentity);
    }

    public ArchitecturePolicyResolvedPath ResolveImport(
        ArchitecturePolicyRootPath root,
        string declaringPath,
        string importPath)
    {
        string platformPath = importPath.Replace('/', Path.DirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(declaringPath)!, platformPath));
        EnsureWithinBoundary(root.BoundaryPath, candidate, importPath);

        string exactPath = ResolveExactPath(root.BoundaryPath, candidate, importPath);
        string physicalPath = ResolvePhysicalPath(root.BoundaryPath, exactPath);
        EnsureWithinBoundary(root.PhysicalBoundaryPath, physicalPath, importPath);
        string fileIdentity = GetRegularFileIdentity(physicalPath, importPath);

        string portableIdentity = Path.GetRelativePath(root.BoundaryPath, exactPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return new ArchitecturePolicyResolvedPath(exactPath, physicalPath, portableIdentity, fileIdentity);
    }

    private static string ResolveExactPath(string boundary, string candidate, string authoredPath)
    {
        string relative = Path.GetRelativePath(boundary, candidate);
        if (relative == ".")
        {
            return Path.GetFullPath(boundary);
        }

        string current = Path.GetFullPath(boundary);
        string[] segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            if (!Directory.Exists(current))
            {
                throw Missing(authoredPath);
            }

            string? exact = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.Ordinal));
            if (exact is not null)
            {
                current = exact;
                continue;
            }

            string? caseInsensitive = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(
                    Path.GetFileName(entry),
                    segment,
                    StringComparison.OrdinalIgnoreCase));
            if (caseInsensitive is not null)
            {
                throw new ArchitecturePolicyImportException(
                    ArchitecturePolicyImportErrorCategory.PathCaseMismatch,
                    $"Policy import '{authoredPath}' does not match on-disk casing at '{caseInsensitive}'.");
            }

            throw Missing(authoredPath);
        }

        if (!File.Exists(current))
        {
            throw Missing(authoredPath);
        }

        return Path.GetFullPath(current);
    }

    private static string ResolvePhysicalPath(string boundary, string path)
    {
        string current = ResolveLink(new DirectoryInfo(Path.GetFullPath(boundary)));
        string relative = Path.GetRelativePath(boundary, path);
        if (relative == ".")
        {
            return current;
        }

        string[] segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            string candidate = Path.Combine(current, segments[index]);
            FileSystemInfo info = index == segments.Length - 1
                ? new FileInfo(candidate)
                : new DirectoryInfo(candidate);
            current = ResolveLink(info);
        }

        return Path.GetFullPath(current);
    }

    private static string ResolveLink(FileSystemInfo info)
    {
        info.Refresh();
        if (info.LinkTarget is null)
        {
            return info.FullName;
        }

        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
    }

    private static void EnsureWithinBoundary(string boundary, string target, string authoredPath)
    {
        string relative = Path.GetRelativePath(boundary, target);
        bool outside = Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        if (outside)
        {
            throw new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.OutOfBoundary,
                $"Policy import '{authoredPath}' resolves outside repository boundary '{boundary}'.");
        }
    }

    private static string GetRegularFileIdentity(string path, string authoredPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsFileIdentity(path, authoredPath);
        }

        return GetUnixFileIdentity(path, authoredPath);
    }

    private static string GetWindowsFileIdentity(string path, string authoredPath)
    {
        using SafeFileHandle handle = CreateFile(
            path,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (handle.IsInvalid
            || GetFileType(handle) != FileTypeDisk
            || !GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw NotRegularFile(authoredPath);
        }

        ulong index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return $"windows:{information.VolumeSerialNumber:X8}:{index:X16}";
    }

    private static string GetUnixFileIdentity(string path, string authoredPath)
    {
        if (OperatingSystem.IsMacOS())
        {
            return GetManagedFileIdentity(path, authoredPath);
        }

        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => GetLinuxX64FileIdentity(path, authoredPath),
            Architecture.Arm64 => GetLinuxArm64FileIdentity(path, authoredPath),
            _ => throw NotRegularFile(authoredPath)
        };
    }

    private static string GetManagedFileIdentity(string path, string authoredPath)
    {
        try
        {
            if (!File.Exists(path)
                || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                throw NotRegularFile(authoredPath);
            }
        }
        catch (IOException)
        {
            throw NotRegularFile(authoredPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw NotRegularFile(authoredPath);
        }

        return $"path:{Path.GetFullPath(path)}";
    }

    private static string GetLinuxX64FileIdentity(string path, string authoredPath)
    {
        if (StatLinuxX64(path, out LinuxX64Stat stat) != 0
            || !IsRegularFile(stat.Mode))
        {
            throw NotRegularFile(authoredPath);
        }

        return $"unix:{stat.Device:X16}:{stat.Inode:X16}";
    }

    private static string GetLinuxArm64FileIdentity(string path, string authoredPath)
    {
        if (StatLinuxArm64(path, out LinuxArm64Stat stat) != 0
            || !IsRegularFile(stat.Mode))
        {
            throw NotRegularFile(authoredPath);
        }

        return $"unix:{stat.Device:X16}:{stat.Inode:X16}";
    }

    private static bool IsRegularFile(uint mode)
    {
        return (mode & FileTypeMask) == RegularFile;
    }

    private static bool IsRegularFile(ushort mode)
    {
        return (mode & (ushort)FileTypeMask) == (ushort)RegularFile;
    }

    private static ArchitecturePolicyImportException Missing(string authoredPath)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.MissingFile,
            $"Policy import file not found: {authoredPath}");
    }

    private static ArchitecturePolicyImportException NotRegularFile(string authoredPath)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            $"Policy import '{authoredPath}' must resolve to a readable regular file.");
    }

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileTypeDisk = 0x00000001;
    private const uint FileTypeMask = 0xF000;
    private const uint RegularFile = 0x8000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle file);

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int StatLinuxX64(string path, out LinuxX64Stat stat);

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int StatLinuxArm64(string path, out LinuxArm64Stat stat);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxX64Stat
    {
        public ulong Device;
        public ulong Inode;
        public ulong LinkCount;
        public uint Mode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] RemainingFields;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxArm64Stat
    {
        public ulong Device;
        public ulong Inode;
        public uint Mode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] RemainingFields;
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
