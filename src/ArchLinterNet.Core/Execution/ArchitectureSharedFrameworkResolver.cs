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
        List<string> resolvedDirectories = new(names.Length);
        List<string> missingFrameworkNames = new();

        foreach (string name in names)
        {
            string? frameworkDirectory = ResolveFrameworkDirectory(name, sharedRoots, fileSystem);
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
            throw new InvalidOperationException(
                "analysis.shared_frameworks named a shared framework that is not installed on this "
                + $"machine: {string.Join(", ", missingFrameworkNames)}. Searched shared-framework roots: "
                + $"{searchedRoots}. Install the corresponding .NET runtime or set {DotNetRootEnvironmentVariable} "
                + "to a directory whose 'shared' subdirectory contains it.");
        }

        return resolvedDirectories;
    }

    private static IReadOnlyList<string> ResolveSharedRoots(
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

    private static string? ResolveFrameworkDirectory(
        string frameworkName, IReadOnlyList<string> sharedRoots, IArchitectureFileSystem fileSystem)
    {
        foreach (string sharedRoot in sharedRoots)
        {
            string frameworkRoot = Path.Combine(sharedRoot, frameworkName);
            if (!fileSystem.DirectoryExists(frameworkRoot))
            {
                continue;
            }

            string? highestVersionDirectory = null;
            Version? highestVersion = null;
            foreach (string versionDirectory in
                     fileSystem.EnumerateDirectories(frameworkRoot, "*", SearchOption.TopDirectoryOnly))
            {
                Version? version = TryParseVersionPrefix(Path.GetFileName(versionDirectory.TrimEnd('/', '\\')));
                if (version is null || (highestVersion is not null && version <= highestVersion))
                {
                    continue;
                }

                highestVersion = version;
                highestVersionDirectory = versionDirectory;
            }

            if (highestVersionDirectory is not null)
            {
                return highestVersionDirectory;
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
