using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Execution;

public interface IArchitectureAssemblyResolutionService
{
    ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds);
}

public sealed class ArchitectureAssemblyResolutionService : IArchitectureAssemblyResolutionService
{
    private const string AssemblySearchPathsEnvVar = "ARCHITECTURE_ASSEMBLY_SEARCH_PATHS";

    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IArchitectureEnvironment _environment;
    private readonly IArchitectureAssemblyLoader _assemblyLoader;

    public ArchitectureAssemblyResolutionService()
        : this(ArchitectureFileSystem.Real, ArchitectureEnvironment.Real, ArchitectureAssemblyLoader.Real)
    {
    }

    public ArchitectureAssemblyResolutionService(
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment,
        IArchitectureAssemblyLoader assemblyLoader)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _assemblyLoader = assemblyLoader;
    }

    public ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds)
    {
        // A scope: project coverage contract needs every discovered project to reach
        // CheckProjectCoverageContract — including ones whose build output is missing, stale,
        // or ambiguous and therefore never resolved to a target assembly — so it can classify
        // them as "unknown"/"unresolved project" instead of nothing at all. Skip the
        // no-assemblies-resolved hard-fail in that case rather than erroring out before the
        // coverage engine ever runs.
        bool projectCoverageCanReportUnresolvedProjects =
            discovery.DiscoveredProjects.Count > 0
            && HasProjectScopeCoverageContract(document, mode, selectedContractIds);

        if (resolveAssemblyOutputs && document.Analysis.TargetAssemblies.Count == 0
            && discovery.Diagnostics.Count > 0 && !projectCoverageCanReportUnresolvedProjects)
        {
            string details = string.Join("; ", discovery.Diagnostics.Select(d => d.Message));
            throw new InvalidOperationException(
                $"Architecture YAML must define analysis.target_assemblies. Project discovery did not resolve any assemblies: {details}");
        }

        return document.Analysis.TargetAssemblies.Count == 0 && projectCoverageCanReportUnresolvedProjects
            ? new ResolutionResult(Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>())
            : ResolveFromDocument(document, repositoryRoot, _fileSystem, _environment, _assemblyLoader);
    }

    public ResolutionResult ResolveFromDocument(
        ArchitectureContractDocument document,
        string? repositoryRoot = null)
    {
        return ResolveFromDocument(document, repositoryRoot, _fileSystem, _environment, _assemblyLoader);
    }

    private static ResolutionResult ResolveFromDocument(
        ArchitectureContractDocument document,
        string? repositoryRoot,
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment,
        IArchitectureAssemblyLoader assemblyLoader)
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
        List<string> missing = new();

        IReadOnlyList<string> probingPaths = ResolveProbingPaths(document, repositoryRoot, fileSystem, environment);

        foreach (string name in names.Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            try
            {
                Assembly assembly = ResolveByName(name.Trim(), probingPaths, fileSystem, assemblyLoader);
                assemblies.Add(assembly);
            }
            catch (InvalidOperationException)
            {
                missing.Add(name.Trim());
            }
        }

        return new ResolutionResult(assemblies, missing, probingPaths.ToArray());
    }

    private static Assembly ResolveByName(
        string assemblyName,
        IReadOnlyList<string> probingPaths,
        IArchitectureFileSystem fileSystem,
        IArchitectureAssemblyLoader assemblyLoader)
    {
        Assembly? alreadyLoaded = assemblyLoader.GetLoadedAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));

        if (alreadyLoaded != null)
        {
            return alreadyLoaded;
        }

        try
        {
            return assemblyLoader.Load(new AssemblyName(assemblyName));
        }
        catch
        {
            foreach (string path in probingPaths)
            {
                string candidate = Path.Combine(path, $"{assemblyName}.dll");
                if (!fileSystem.FileExists(candidate))
                {
                    continue;
                }

                try
                {
                    return assemblyLoader.LoadFrom(candidate);
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

    private static IReadOnlyList<string> ResolveProbingPaths(
        ArchitectureContractDocument document,
        string? repositoryRoot,
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment)
    {
        List<string> result = new();

        foreach (string path in ResolveEnvProbingPaths(fileSystem, environment))
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

            if (!fileSystem.DirectoryExists(resolvedPath))
            {
                continue;
            }

            result.Add(resolvedPath);
        }

        string appBaseDirectory = environment.BaseDirectory;
        if (fileSystem.DirectoryExists(appBaseDirectory))
        {
            result.Add(appBaseDirectory);
        }

        if (!string.IsNullOrWhiteSpace(repositoryRoot) && fileSystem.DirectoryExists(repositoryRoot))
        {
            result.Add(repositoryRoot);
        }

        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            string artifactsBin = Path.Combine(repositoryRoot, "artifacts", "bin");
            if (fileSystem.DirectoryExists(artifactsBin))
            {
                result.Add(artifactsBin);
            }

            string repoBin = Path.Combine(repositoryRoot, "bin");
            if (fileSystem.DirectoryExists(repoBin))
            {
                result.Add(repoBin);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ResolveEnvProbingPaths(
        IArchitectureFileSystem fileSystem, IArchitectureEnvironment environment)
    {
        string? raw = environment.GetEnvironmentVariable(AssemblySearchPathsEnvVar);
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

            if (fileSystem.DirectoryExists(normalized))
            {
                yield return normalized;
            }
        }
    }

    // mode is null for callers (e.g. ArchitectureBaselineService with request.Mode "all") that
    // don't pin a single mode up front; in that case both strict and audit project-scope coverage
    // contracts are considered, mirroring how such callers later execute both modes themselves.
    private static bool HasProjectScopeCoverageContract(
        ArchitectureContractDocument document, string? mode, HashSet<string>? selectedContractIds)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        IEnumerable<IArchitectureContract> coverageContracts = mode != null
            ? catalog.ContractsFor(mode, "coverage")
            : catalog.ContractsFor("strict", "coverage").Concat(catalog.ContractsFor("audit", "coverage"));

        return coverageContracts
            .OfType<ArchitectureCoverageContract>()
            .Any(contract => string.Equals(contract.Scope, "project", StringComparison.Ordinal)
                && IsContractIdSelected(contract.Id, selectedContractIds));
    }

    private static bool IsContractIdSelected(string? contractId, HashSet<string>? selectedContractIds)
    {
        return selectedContractIds == null || selectedContractIds.Count == 0
            || (contractId != null && selectedContractIds.Contains(contractId));
    }
}

public sealed record ResolutionResult(
    IReadOnlyCollection<Assembly> ResolvedAssemblies,
    IReadOnlyCollection<string> MissingAssemblyNames,
    IReadOnlyCollection<string> AssemblyProbingPaths);
