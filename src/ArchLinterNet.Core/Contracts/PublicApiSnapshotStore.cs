using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public sealed class PublicApiSnapshotStore(IArchitectureFileSystem fileSystem) : IPublicApiSnapshotStore
{
    public string ResolvePath(string policyPath, string snapshotPath)
    {
        string boundary = PublicApiSnapshotResolver.ResolveBoundary(policyPath);
        return PublicApiSnapshotResolver.ResolveSnapshotPath(boundary, snapshotPath, "The requested snapshot");
    }

    public bool Exists(string resolvedPath)
    {
        return fileSystem.FileExists(resolvedPath);
    }

    // Two paths merely both existing does not prove they are the same file: a case-sensitive
    // filesystem can legitimately hold "Surface.txt" and "surface.txt" as two distinct entries.
    // The only reliable signal is what the directory itself actually contains: enumerate its real
    // entries and count how many case-insensitively match the shared filename. Exactly one such
    // entry means both spellings resolve to it (a case-insensitive filesystem, or a case-sensitive
    // one that only ever stored the file under one of the two spellings); two means the filesystem
    // is genuinely holding both as separate files; zero means neither exists.
    public bool IsSameFile(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return true;
        }

        string? firstDirectory = Path.GetDirectoryName(first);
        string? secondDirectory = Path.GetDirectoryName(second);
        if (firstDirectory == null || secondDirectory == null
            || !string.Equals(firstDirectory, secondDirectory, StringComparison.OrdinalIgnoreCase)
            || !fileSystem.DirectoryExists(secondDirectory))
        {
            return false;
        }

        string sharedName = Path.GetFileName(second);
        int matchingEntries = fileSystem
            .EnumerateFiles(secondDirectory, "*", SearchOption.TopDirectoryOnly)
            .Count(entry => string.Equals(Path.GetFileName(entry), sharedName, StringComparison.OrdinalIgnoreCase));

        return matchingEntries == 1;
    }

    public PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath)
    {
        return PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), authoredPath);
    }
}
