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
    // Conversely, "exactly one real entry matches either spelling" does not prove it either: on a
    // case-sensitive filesystem holding only "surface.txt", a query for "Surface.txt" still counts
    // as one case-insensitive match against that entry even though "Surface.txt" does not itself
    // exist — a case-sensitive FileExists("Surface.txt") correctly fails, and only that failure
    // reveals it. Both signals are required together: the directory listing rules out two distinct
    // real entries, and an exact-case existence check on *both* spellings rules out a lone entry
    // whose casing only happens to match one of them.
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

        return matchingEntries == 1 && fileSystem.FileExists(first) && fileSystem.FileExists(second);
    }

    public PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath)
    {
        return PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), authoredPath);
    }
}
