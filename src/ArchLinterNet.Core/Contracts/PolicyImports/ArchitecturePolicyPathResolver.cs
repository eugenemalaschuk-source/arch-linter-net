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

        return new ArchitecturePolicyRootPath(rootPath, exactRoot, physicalRoot, boundary, physicalBoundary);
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

        string portableIdentity = Path.GetRelativePath(root.BoundaryPath, exactPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return new ArchitecturePolicyResolvedPath(exactPath, physicalPath, portableIdentity);
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

    private static ArchitecturePolicyImportException Missing(string authoredPath)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.MissingFile,
            $"Policy import file not found: {authoredPath}");
    }
}
