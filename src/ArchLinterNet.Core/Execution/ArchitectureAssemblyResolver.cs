using System.Reflection;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public static class ArchitectureAssemblyResolver
{
    private const string AssemblySearchPathsEnvVar = "ARCHITECTURE_ASSEMBLY_SEARCH_PATHS";

    public static IReadOnlyCollection<Assembly> ResolveFromDocument(ArchitectureContractDocument document,
        string? repositoryRoot = null)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<string> names = document.Analysis.TargetAssemblies;
        if (names.Count == 0)
        {
            throw new InvalidOperationException("Architecture YAML must define analysis.target_assemblies.");
        }

        List<Assembly> assemblies = new(names.Count);

        IReadOnlyList<string> probingPaths = ResolveProbingPaths(document, repositoryRoot);

        foreach (string name in names.Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            Assembly assembly = ResolveByName(name.Trim(), probingPaths);
            assemblies.Add(assembly);
        }

        return assemblies;
    }

    private static Assembly ResolveByName(string assemblyName, IReadOnlyList<string> probingPaths)
    {
        Assembly? alreadyLoaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));

        if (alreadyLoaded != null)
        {
            return alreadyLoaded;
        }

        try
        {
            return Assembly.Load(new AssemblyName(assemblyName));
        }
        catch
        {
            foreach (string path in probingPaths)
            {
                string candidate = Path.Combine(path, $"{assemblyName}.dll");
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return Assembly.LoadFrom(candidate);
                }
                catch
                {
                }
            }
        }

        string probes = probingPaths.Count == 0 ? "<none>" : string.Join(", ", probingPaths);
        throw new InvalidOperationException(
            $"Failed to resolve target assembly '{assemblyName}' from architecture YAML. Probing paths: {probes}");
    }

    private static IReadOnlyList<string> ResolveProbingPaths(ArchitectureContractDocument document,
        string? repositoryRoot)
    {
        List<string> result = new();

        foreach (string path in ResolveEnvProbingPaths())
        {
            result.Add(path);
        }

        foreach (string rawPath in document.Analysis.AssemblySearchPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string normalizedPath = rawPath.Trim();
            string resolvedPath = Path.IsPathRooted(normalizedPath)
                ? normalizedPath
                : string.IsNullOrWhiteSpace(repositoryRoot)
                    ? normalizedPath
                    : Path.GetFullPath(Path.Combine(repositoryRoot, normalizedPath));

            if (!Directory.Exists(resolvedPath))
            {
                continue;
            }

            result.Add(resolvedPath);
        }

        string appBaseDirectory = AppContext.BaseDirectory;
        if (Directory.Exists(appBaseDirectory))
        {
            result.Add(appBaseDirectory);
        }

        if (!string.IsNullOrWhiteSpace(repositoryRoot) && Directory.Exists(repositoryRoot))
        {
            result.Add(repositoryRoot);
        }

        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            string artifactsBin = Path.Combine(repositoryRoot, "artifacts", "bin");
            if (Directory.Exists(artifactsBin))
            {
                result.Add(artifactsBin);
            }

            string repoBin = Path.Combine(repositoryRoot, "bin");
            if (Directory.Exists(repoBin))
            {
                result.Add(repoBin);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ResolveEnvProbingPaths()
    {
        string? raw = Environment.GetEnvironmentVariable(AssemblySearchPathsEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (string path in raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string normalized = path.Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            if (Directory.Exists(normalized))
            {
                yield return normalized;
            }
        }
    }
}
