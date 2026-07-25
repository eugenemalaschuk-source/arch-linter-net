using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Resolves every contract's `api_snapshot` at policy load time, so a missing, escaping, oversized,
// or unparsable snapshot fails loudly before any analysis runs instead of silently degrading the
// contract into "declares nothing" (the same posture PublicApiSurfaceValidator already takes for a
// typo'd assembly name).
internal static class PublicApiSnapshotResolver
{
    public static void Resolve(
        ArchitectureContractDocument document,
        string policyPath,
        IArchitectureFileSystem fileSystem)
    {
        string boundary = ResolveBoundary(policyPath);

        foreach (ArchitecturePublicApiSurfaceContract contract in document.Contracts.StrictPublicApiSurface
                     .Concat(document.Contracts.AuditPublicApiSurface))
        {
            if (string.IsNullOrWhiteSpace(contract.ApiSnapshot))
            {
                continue;
            }

            contract.ResolvedSnapshotEntries = LoadEntries(contract, boundary, fileSystem);
        }
    }

    // Mirrors ArchitecturePolicyPathResolver.ResolveRoot: the boundary is the policy's directory,
    // or its parent when the policy lives in an `architecture/` folder. That is what lets a policy
    // at architecture/dependencies.arch.yml reference architecture/api/module-api.txt exactly as
    // the repository lays it out.
    public static string ResolveBoundary(string policyPath)
    {
        string fullPath = Path.GetFullPath(policyPath);
        string policyDirectory = Path.GetDirectoryName(fullPath) ?? fullPath;
        return string.Equals(Path.GetFileName(policyDirectory), "architecture", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(policyDirectory) ?? policyDirectory
            : policyDirectory;
    }

    // Repository-local means: relative, non-rooted, and still inside the boundary once normalized.
    // Returns the absolute path so callers can read or write it.
    public static string ResolveSnapshotPath(string boundary, string snapshotPath, string subjectDescription)
    {
        if (Path.IsPathRooted(snapshotPath))
        {
            throw new InvalidOperationException(
                $"{subjectDescription} declares an absolute public API snapshot path '{snapshotPath}'. " +
                "Snapshot paths must be relative and stay inside the policy boundary so a policy " +
                "cannot read or write reviewed API state from outside the repository.");
        }

        string platformPath = snapshotPath.Replace('/', Path.DirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(boundary, platformPath));
        string relative = Path.GetRelativePath(boundary, candidate);

        bool escapes = Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

        if (escapes)
        {
            throw new InvalidOperationException(
                $"{subjectDescription} declares a public API snapshot path '{snapshotPath}' that resolves " +
                $"outside the policy boundary '{boundary}'. Snapshot paths must stay repository-local.");
        }

        return candidate;
    }

    private static IReadOnlyList<PublicApiSnapshotEntry> LoadEntries(
        ArchitecturePublicApiSurfaceContract contract,
        string boundary,
        IArchitectureFileSystem fileSystem)
    {
        string subject = $"Public API surface contract '{contract.Name}'";
        string resolvedPath = ResolveSnapshotPath(boundary, contract.ApiSnapshot!, subject);

        if (!fileSystem.FileExists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"{subject} references a public API snapshot '{contract.ApiSnapshot}' that does not exist " +
                $"(resolved to '{resolvedPath}'). Run 'arch-linter-net public-api capture' to create it, " +
                "otherwise a missing snapshot would silently reduce the contract to declaring nothing.");
        }

        try
        {
            return PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), contract.ApiSnapshot!).Entries;
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{subject}: {exception.Message}", exception);
        }
    }
}
