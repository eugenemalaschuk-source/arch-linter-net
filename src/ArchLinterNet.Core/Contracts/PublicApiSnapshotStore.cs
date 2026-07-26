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
    // filesystem can legitimately hold "Surface.txt" and "surface.txt" as two distinct entries, and
    // the same is true one level up — "/repo/api/surface.txt" and "/repo/API/surface.txt" can both
    // be real, distinct files. Comparing directory names with OrdinalIgnoreCase (instead of
    // establishing their real identity the same way we establish the leaf file's) would let that
    // slip through as a false match. So identity is established component-by-component, walking up
    // to the root: at each level, "exactly one real entry matches either spelling" alone is not
    // proof either — on a case-sensitive filesystem holding only one spelling, a query for the other
    // still counts as one case-insensitive match against that entry even though the queried spelling
    // does not itself exist. Both signals are required together at every level: the directory
    // listing rules out two distinct real entries, and an exact-case existence check on *both*
    // spellings rules out a lone entry whose casing only happens to match one of them.
    public bool IsSameFile(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return true;
        }

        string? firstDirectory = Path.GetDirectoryName(first);
        string? secondDirectory = Path.GetDirectoryName(second);
        string firstName = Path.GetFileName(first);
        string secondName = Path.GetFileName(second);
        if (firstDirectory == null || secondDirectory == null
            || !string.Equals(firstName, secondName, StringComparison.OrdinalIgnoreCase)
            || !IsSameDirectory(firstDirectory, secondDirectory))
        {
            return false;
        }

        return HasExactlyOneMatch(secondDirectory, secondName, isDirectory: false)
            && fileSystem.FileExists(first) && fileSystem.FileExists(second);
    }

    private bool IsSameDirectory(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return true;
        }

        string? firstParent = Path.GetDirectoryName(first);
        string? secondParent = Path.GetDirectoryName(second);
        string firstName = Path.GetFileName(first);
        string secondName = Path.GetFileName(second);

        // A root path (e.g. "/" or "C:\") has no parent and no filename component to compare — it is
        // unambiguous, so exact-case existence on both spellings is the whole check.
        if (firstParent == null || secondParent == null
            || firstName.Length == 0 || secondName.Length == 0)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
                && fileSystem.DirectoryExists(first) && fileSystem.DirectoryExists(second);
        }

        if (!string.Equals(firstName, secondName, StringComparison.OrdinalIgnoreCase)
            || !IsSameDirectory(firstParent, secondParent))
        {
            return false;
        }

        return HasExactlyOneMatch(secondParent, secondName, isDirectory: true)
            && fileSystem.DirectoryExists(first) && fileSystem.DirectoryExists(second);
    }

    private bool HasExactlyOneMatch(string parentDirectory, string name, bool isDirectory)
    {
        if (!fileSystem.DirectoryExists(parentDirectory))
        {
            return false;
        }

        IEnumerable<string> entries = isDirectory
            ? fileSystem.EnumerateDirectories(parentDirectory, "*", SearchOption.TopDirectoryOnly)
            : fileSystem.EnumerateFiles(parentDirectory, "*", SearchOption.TopDirectoryOnly);

        return entries.Count(entry => string.Equals(Path.GetFileName(entry), name, StringComparison.OrdinalIgnoreCase)) == 1;
    }

    public PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath)
    {
        return PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), authoredPath);
    }
}
