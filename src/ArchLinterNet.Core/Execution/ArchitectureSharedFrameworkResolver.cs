using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Execution;

// Resolves analysis.shared_frameworks names (e.g. "Microsoft.AspNetCore.App") to the installed
// shared-framework directory on the host machine, so post-build (--ensure-built) resolution can
// probe for consumer-referenced framework assemblies that are absent from the CLI host's own
// trusted platform assembly list. See the assembly-resolution spec's shared-framework requirement.
internal static class ArchitectureSharedFrameworkResolver
{
    private const string DotNetRootEnvironmentVariable = "DOTNET_ROOT";
    private const string DotNetRootX86EnvironmentVariable = "DOTNET_ROOT(X86)";
    private const string SharedDirectoryName = "shared";

    public static IReadOnlyList<string> ResolveProbingPaths(
        IReadOnlyList<string> sharedFrameworkNames,
        string? targetFrameworkMoniker,
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment)
    {
        string[] names = sharedFrameworkNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (names.Length == 0)
        {
            return Array.Empty<string>();
        }

        IReadOnlyList<string> sharedRoots = ResolveSharedRoots(fileSystem, environment);
        int? anchorMajorVersion = ResolveAnchorMajorVersion(targetFrameworkMoniker, environment);
        List<string> resolvedDirectories = new(names.Length);
        List<string> missingFrameworkNames = new();

        foreach (string name in names)
        {
            string? frameworkDirectory = ResolveFrameworkDirectory(name, sharedRoots, fileSystem, anchorMajorVersion);
            if (frameworkDirectory is null)
            {
                missingFrameworkNames.Add(name);
                continue;
            }

            resolvedDirectories.Add(frameworkDirectory);
        }

        if (missingFrameworkNames.Count > 0)
        {
            string searchedRoots = sharedRoots.Count == 0 ? "<none>" : string.Join(", ", sharedRoots);
            string majorVersionClause = anchorMajorVersion is int major
                ? $" compatible with major version {major}"
                : string.Empty;
            throw new InvalidOperationException(
                "analysis.shared_frameworks named a shared framework that is not installed on this "
                + $"machine{majorVersionClause}: {string.Join(", ", missingFrameworkNames)}. Searched "
                + $"shared-framework roots: {searchedRoots}. Install the corresponding .NET runtime or set "
                + $"{DotNetRootEnvironmentVariable} to a directory whose 'shared' subdirectory contains it.");
        }

        return resolvedDirectories;
    }

    private static string[] ResolveSharedRoots(
        IArchitectureFileSystem fileSystem, IArchitectureEnvironment environment)
    {
        List<string> roots = new();
        AddSharedRootFromDotNetRoot(environment.GetEnvironmentVariable(DotNetRootEnvironmentVariable), fileSystem, roots);
        AddSharedRootFromDotNetRoot(environment.GetEnvironmentVariable(DotNetRootX86EnvironmentVariable), fileSystem, roots);

        // The currently running runtime's own directory is already the shared-framework store
        // itself: ".../shared/Microsoft.NETCore.App/<version>" -> two levels up is ".../shared".
        string runtimeDirectory = environment.RuntimeDirectory;
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            string trimmed = runtimeDirectory.TrimEnd('/', '\\');
            string? runtimeSharedRoot = Path.GetDirectoryName(Path.GetDirectoryName(trimmed));
            if (!string.IsNullOrWhiteSpace(runtimeSharedRoot) && fileSystem.DirectoryExists(runtimeSharedRoot))
            {
                roots.Add(runtimeSharedRoot);
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddSharedRootFromDotNetRoot(
        string? dotNetRoot, IArchitectureFileSystem fileSystem, List<string> roots)
    {
        if (string.IsNullOrWhiteSpace(dotNetRoot))
        {
            return;
        }

        string sharedRoot = Path.Combine(dotNetRoot.Trim(), SharedDirectoryName);
        if (fileSystem.DirectoryExists(sharedRoot))
        {
            roots.Add(sharedRoot);
        }
    }

    // The .NET host's default roll-forward policy never crosses a major version and prefers a
    // release build over a prerelease one. Anchoring shared-framework selection the same way stops
    // a machine that also has e.g. Microsoft.AspNetCore.App 11.0.0-preview.* installed from being
    // silently selected for a net10 consumer merely because "11" sorts higher than "10".
    private static int? ResolveAnchorMajorVersion(string? targetFrameworkMoniker, IArchitectureEnvironment environment)
    {
        int? fromTargetFramework = TryParseMajorFromTargetFrameworkMoniker(targetFrameworkMoniker);
        if (fromTargetFramework is not null)
        {
            return fromTargetFramework;
        }

        string runtimeDirectory = environment.RuntimeDirectory;
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return null;
        }

        string versionSegment = Path.GetFileName(runtimeDirectory.TrimEnd('/', '\\'));
        return TryParseVersionPrefix(versionSegment)?.Major;
    }

    // Modern TFMs ("net8.0", "net10.0-windows") always carry a dot after the major version; older
    // monikers without one ("net48", "netcoreapp3.1" has a dot but isn't "netN") are intentionally
    // left unrecognized rather than misparsed into an unrelated major number.
    private static int? TryParseMajorFromTargetFrameworkMoniker(string? targetFrameworkMoniker)
    {
        if (string.IsNullOrWhiteSpace(targetFrameworkMoniker))
        {
            return null;
        }

        string trimmed = targetFrameworkMoniker.Trim();
        if (!trimmed.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string remainder = trimmed[3..];
        int dotIndex = remainder.IndexOf('.');
        if (dotIndex < 0)
        {
            return null;
        }

        return int.TryParse(remainder[..dotIndex], out int major) ? major : null;
    }

    private static string? ResolveFrameworkDirectory(
        string frameworkName, IReadOnlyList<string> sharedRoots, IArchitectureFileSystem fileSystem,
        int? anchorMajorVersion)
    {
        foreach (string sharedRoot in sharedRoots)
        {
            string frameworkRoot = Path.Combine(sharedRoot, frameworkName);
            if (!fileSystem.DirectoryExists(frameworkRoot))
            {
                continue;
            }

            (string Directory, Version Version)? bestStable = null;
            (string Directory, Version Version)? bestPrerelease = null;
            foreach (string versionDirectory in
                     fileSystem.EnumerateDirectories(frameworkRoot, "*", SearchOption.TopDirectoryOnly))
            {
                string versionName = Path.GetFileName(versionDirectory.TrimEnd('/', '\\'));
                Version? version = TryParseVersionPrefix(versionName);
                if (version is null || (anchorMajorVersion is int major && version.Major != major))
                {
                    continue;
                }

                if (versionName.Contains('-', StringComparison.Ordinal))
                {
                    if (bestPrerelease is null || version > bestPrerelease.Value.Version)
                    {
                        bestPrerelease = (versionDirectory, version);
                    }
                }
                else if (bestStable is null || version > bestStable.Value.Version)
                {
                    bestStable = (versionDirectory, version);
                }
            }

            // A release build is always preferred over a prerelease one, even a numerically higher
            // prerelease; a prerelease is only used when it is the sole candidate for this framework.
            string? selected = (bestStable ?? bestPrerelease)?.Directory;
            if (selected is not null)
            {
                return selected;
            }
        }

        return null;
    }

    private static Version? TryParseVersionPrefix(string directoryName)
    {
        int prereleaseSeparator = directoryName.IndexOf('-');
        string numericPart = prereleaseSeparator >= 0 ? directoryName[..prereleaseSeparator] : directoryName;
        return Version.TryParse(numericPart, out Version? version) ? version : null;
    }
}
