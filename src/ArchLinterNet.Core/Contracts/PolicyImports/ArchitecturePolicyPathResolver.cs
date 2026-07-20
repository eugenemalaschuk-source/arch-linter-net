using System.Diagnostics.CodeAnalysis;
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
        string physicalBoundary = ResolvePhysicalPath(boundary, boundary, rootPath);
        string physicalRoot = ResolvePhysicalPath(boundary, exactRoot, rootPath);
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
        string physicalPath = ResolvePhysicalPath(root.BoundaryPath, exactPath, importPath);
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
            string[] entries = EnumerateEntries(current, authoredPath);
            string? exact = entries
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.Ordinal));
            if (exact is not null)
            {
                current = exact;
                continue;
            }

            string? caseInsensitive = entries
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

        return EnsureExistingPath(current, authoredPath);
    }

    private static string[] EnumerateEntries(string path, string authoredPath)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(path).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(authoredPath);
        }
        catch (IOException)
        {
            throw Unreadable(authoredPath);
        }
    }

    private static string EnsureExistingPath(string path, string authoredPath)
    {
        try
        {
            _ = File.GetAttributes(path);
            return Path.GetFullPath(path);
        }
        catch (FileNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (DirectoryNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(authoredPath);
        }
        catch (IOException)
        {
            throw Unreadable(authoredPath);
        }
    }

    private static string ResolvePhysicalPath(string boundary, string path, string authoredPath)
    {
        try
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
        catch (FileNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (DirectoryNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(authoredPath);
        }
        catch (IOException)
        {
            throw Unreadable(authoredPath);
        }
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
        if (handle.IsInvalid)
        {
            throw ClassifyWindowsNativeFailure(authoredPath, Marshal.GetLastPInvokeError());
        }

        if (GetFileType(handle) != FileTypeDisk)
        {
            throw NotRegularFile(authoredPath);
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw ClassifyWindowsNativeFailure(authoredPath, Marshal.GetLastPInvokeError());
        }

        ulong index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return $"windows:{information.VolumeSerialNumber:X8}:{index:X16}";
    }

    private static string GetUnixFileIdentity(string path, string authoredPath)
    {
        if (OperatingSystem.IsMacOS())
        {
            return GetMacOSFileIdentity(path, authoredPath);
        }

        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => GetLinuxX64FileIdentity(path, authoredPath),
            Architecture.Arm64 => GetLinuxArm64FileIdentity(path, authoredPath),
            _ => throw NotRegularFile(authoredPath)
        };
    }

    [ExcludeFromCodeCoverage]
    private static string GetMacOSFileIdentity(string path, string authoredPath)
    {
        try
        {
            _ = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (DirectoryNotFoundException)
        {
            throw Missing(authoredPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(authoredPath);
        }
        catch (IOException)
        {
            throw Unreadable(authoredPath);
        }

        var attributes = new DarwinAttributeList
        {
            BitmapCount = AttributeBitMapCount,
            CommonAttributes = CommonDeviceAttribute | CommonObjectTypeAttribute | CommonFileIdAttribute,
        };
        if (GetAttributeList(
                path,
                ref attributes,
                out DarwinFileIdentityAttributes identity,
                (nuint)Marshal.SizeOf<DarwinFileIdentityAttributes>(),
                options: 0) != 0)
        {
            throw ClassifyUnixNativeFailure(authoredPath, Marshal.GetLastPInvokeError());
        }

        if (identity.ObjectType != DarwinRegularFile)
        {
            throw NotRegularFile(authoredPath);
        }

        return $"darwin:{identity.Device:X8}:{identity.FileId:X16}";
    }

    private static string GetLinuxX64FileIdentity(string path, string authoredPath)
    {
        if (StatLinuxX64(path, out LinuxX64Stat stat) != 0)
        {
            throw ClassifyUnixNativeFailure(authoredPath, Marshal.GetLastPInvokeError());
        }

        if (!IsRegularFile(stat.Mode))
        {
            throw NotRegularFile(authoredPath);
        }

        return $"unix:{stat.Device:X16}:{stat.Inode:X16}";
    }

    private static string GetLinuxArm64FileIdentity(string path, string authoredPath)
    {
        if (StatLinuxArm64(path, out LinuxArm64Stat stat) != 0)
        {
            throw ClassifyUnixNativeFailure(authoredPath, Marshal.GetLastPInvokeError());
        }

        if (!IsRegularFile(stat.Mode))
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

    private static ArchitecturePolicyImportException Unreadable(string authoredPath)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.UnreadableFile,
            $"Policy import '{authoredPath}' is not readable.");
    }

    internal static ArchitecturePolicyImportException ClassifyWindowsNativeFailure(string authoredPath, int error)
    {
        return error switch
        {
            2 or 3 => Missing(authoredPath),
            5 => Unreadable(authoredPath),
            _ => PlatformFailure(authoredPath, "Win32", error)
        };
    }

    internal static ArchitecturePolicyImportException ClassifyUnixNativeFailure(string authoredPath, int error)
    {
        return error switch
        {
            2 or 20 => Missing(authoredPath),
            1 or 13 => Unreadable(authoredPath),
            _ => PlatformFailure(authoredPath, "errno", error)
        };
    }

    private static ArchitecturePolicyImportException PlatformFailure(string authoredPath, string errorDomain, int error)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.PlatformFailure,
            $"Policy import '{authoredPath}' could not be inspected ({errorDomain} {error}).");
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
    private const ushort AttributeBitMapCount = 5;
    private const uint CommonDeviceAttribute = 0x00000002;
    private const uint CommonObjectTypeAttribute = 0x00000008;
    private const uint CommonFileIdAttribute = 0x02000000;
    private const uint DarwinRegularFile = 1;

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

    [DllImport("libc", SetLastError = true, EntryPoint = "getattrlist")]
    private static extern int GetAttributeList(
        string path,
        ref DarwinAttributeList attributes,
        out DarwinFileIdentityAttributes attributeBuffer,
        nuint attributeBufferSize,
        uint options);

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
