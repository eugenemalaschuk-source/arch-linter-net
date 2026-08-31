using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAssemblyResolutionService : IArchitectureAssemblyResolutionService
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
        HashSet<string>? selectedContractIds,
        CancellationToken cancellationToken = default)
    {
        bool requiresExactMetricArtifact = ArchitectureMetricProjectOwnership.RequiresExactArtifactBinding(document);
        return Resolve(document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds,
            forceIsolatedLoading: requiresExactMetricArtifact,
            exactAssemblyPaths: requiresExactMetricArtifact ? discovery.ResolvedAssemblyPaths : null,
            expectedArtifactContentDigests: null, cancellationToken);
    }

    public ResolutionResult ResolvePostBuild(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds,
        IReadOnlyDictionary<string, string>? expectedArtifactContentDigests = null,
        CancellationToken cancellationToken = default)
    {
        return Resolve(document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds,
            forceIsolatedLoading: true, exactAssemblyPaths: discovery.ResolvedAssemblyPaths,
            expectedArtifactContentDigests, cancellationToken);
    }

    private ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds,
        bool forceIsolatedLoading,
        IReadOnlyDictionary<string, string>? exactAssemblyPaths,
        IReadOnlyDictionary<string, string>? expectedArtifactContentDigests,
        CancellationToken cancellationToken)
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
            // A real project graph was discovered (analysis.projects), it just didn't resolve to
            // build output — report those assembly names as missing rather than throwing an
            // untyped configuration error. This lets build-state preflight (see #362) produce a
            // typed missing/stale-artifact diagnostic for a clean checkout even when no project-
            // scope coverage contract is active to make resolution tolerant of it. Only throw the
            // generic misconfiguration error when discovery found no projects at all — there is
            // genuinely nothing to identify what should be validated in that case.
            if (discovery.DiscoveredProjects.Count > 0)
            {
                string[] discoveredAssemblyNames = discovery.DiscoveredProjects
                    .Select(project => project.AssemblyName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new ResolutionResult(Array.Empty<Assembly>(), discoveredAssemblyNames, Array.Empty<string>());
            }

            string details = string.Join("; ", discovery.Diagnostics.Select(d => d.Message));
            throw new InvalidOperationException(
                $"Architecture YAML must define analysis.target_assemblies. Project discovery did not resolve any assemblies: {details}");
        }

        if (!resolveAssemblyOutputs && document.Analysis.TargetAssemblies.Count == 0)
        {
            return new ResolutionResult(Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>());
        }

        return document.Analysis.TargetAssemblies.Count == 0 && projectCoverageCanReportUnresolvedProjects
            ? new ResolutionResult(Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>())
            : ResolveFromDocument(document, repositoryRoot, _fileSystem, _environment, _assemblyLoader,
                forceIsolatedLoading, exactAssemblyPaths, discovery.AssemblySearchPaths,
                expectedArtifactContentDigests, cancellationToken);
    }

    public ResolutionResult ResolveFromDocument(
        ArchitectureContractDocument document,
        string? repositoryRoot = null,
        CancellationToken cancellationToken = default)
    {
        return ResolveFromDocument(document, repositoryRoot, _fileSystem, _environment, _assemblyLoader,
            forceIsolatedLoading: false, exactAssemblyPaths: null, additionalProbingPaths: null,
            expectedArtifactContentDigests: null, cancellationToken);
    }

    private static ResolutionResult ResolveFromDocument(
        ArchitectureContractDocument document,
        string? repositoryRoot,
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment,
        IArchitectureAssemblyLoader assemblyLoader,
        bool forceIsolatedLoading,
        IReadOnlyDictionary<string, string>? exactAssemblyPaths,
        IReadOnlyCollection<string>? additionalProbingPaths,
        IReadOnlyDictionary<string, string>? expectedArtifactContentDigests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<string> names = document.Analysis.TargetAssemblies;
        if (names.Count == 0)
        {
            throw new InvalidOperationException("Architecture YAML must define analysis.target_assemblies.");
        }

        List<Assembly> assemblies = new(names.Count);
        List<string> selectedAssemblyArtifactPaths = new(names.Count);
        Dictionary<Assembly, string> resolvedAssemblyArtifactPaths = new();
        List<string> missing = new();
        int assemblyLoads = 0;

        // Shared-framework probing applies to every isolated exact-artifact scope: post-build
        // analysis and ordinary project metrics both load their selected output rather than a
        // same-simple-name host assembly. Non-isolated resolution keeps its existing boundary.
        IEnumerable<string> sharedFrameworkProbingPaths = forceIsolatedLoading
            ? ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
                document.Analysis.SharedFrameworks, document.Analysis.TargetFramework,
                ExtractDiscoveredTargetFrameworks(exactAssemblyPaths, names), fileSystem, environment)
            : Array.Empty<string>();
        IReadOnlyList<string> probingPaths = ResolveProbingPaths(
            document,
            repositoryRoot,
            fileSystem,
            environment,
            additionalProbingPaths,
            sharedFrameworkProbingPaths);
        IReadOnlyDictionary<string, string> exactPaths = exactAssemblyPaths
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        IArchitectureAssemblyLoadScope? isolatedLoadScope = forceIsolatedLoading
            ? assemblyLoader.CreateIsolatedLoadScope(probingPaths, exactPaths, expectedArtifactContentDigests)
            : null;

        // Ownership of isolatedLoadScope transfers to the returned ResolutionResult only when this
        // method actually returns one. Any exceptional exit from the loop below (in particular
        // cancellation, but any other unexpected throw too) must dispose the scope here instead —
        // nothing downstream can reach it to clean it up, since no ResolutionResult/
        // ArchitectureAnalysisContext was ever constructed to own it.
        try
        {
            foreach (string name in names.Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ResolvedAssembly resolved = ResolveByName(
                        name.Trim(), probingPaths, fileSystem, assemblyLoader, isolatedLoadScope, exactPaths);
                    assemblies.Add(resolved.Assembly);
                    if (resolved.ArtifactPath is not null)
                    {
                        string artifactPath = Path.GetFullPath(resolved.ArtifactPath);
                        selectedAssemblyArtifactPaths.Add(artifactPath);
                        resolvedAssemblyArtifactPaths.TryAdd(resolved.Assembly, artifactPath);
                    }

                    assemblyLoads += resolved.WasLoaded ? 1 : 0;
                }
                catch (InvalidOperationException)
                {
                    missing.Add(name.Trim());
                }
            }

        }
        catch
        {
            isolatedLoadScope?.Dispose();
            throw;
        }

        return new ResolutionResult(
            assemblies,
            missing,
            probingPaths.ToArray(),
            assemblyLoads,
            isolatedLoadScope)
        {
            SelectedAssemblyArtifactPaths = selectedAssemblyArtifactPaths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ResolvedAssemblyArtifactPaths = resolvedAssemblyArtifactPaths,
        };
    }

    private static ResolvedAssembly ResolveByName(
        string assemblyName,
        IReadOnlyList<string> probingPaths,
        IArchitectureFileSystem fileSystem,
        IArchitectureAssemblyLoader assemblyLoader,
        IArchitectureAssemblyLoadScope? isolatedLoadScope,
        IReadOnlyDictionary<string, string> exactAssemblyPaths)
    {
        if (isolatedLoadScope != null)
        {
            if (!exactAssemblyPaths.TryGetValue(assemblyName, out string? exactPath))
            {
                throw new InvalidOperationException(
                    $"No verified post-build artifact was discovered for target assembly '{assemblyName}'.");
            }

            if (!fileSystem.FileExists(exactPath))
            {
                throw new InvalidOperationException(
                    $"Verified post-build artifact '{exactPath}' for target assembly '{assemblyName}' no longer exists.");
            }

            return new ResolvedAssembly(isolatedLoadScope.LoadFrom(exactPath), WasLoaded: true, exactPath);
        }

        HashSet<Assembly> loadedBeforeResolution = assemblyLoader.GetLoadedAssemblies().ToHashSet();
        Assembly? alreadyLoaded = loadedBeforeResolution
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));

        if (alreadyLoaded != null)
        {
            return new ResolvedAssembly(alreadyLoaded, WasLoaded: false, GetAssemblyLocation(alreadyLoaded));
        }

        try
        {
            Assembly assembly = assemblyLoader.Load(new AssemblyName(assemblyName));
            return new ResolvedAssembly(
                assembly,
                WasLoaded: !loadedBeforeResolution.Contains(assembly),
                GetAssemblyLocation(assembly));
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
                    bool wasLoadedBeforePathProbe = assemblyLoader.GetLoadedAssemblies()
                        .Any(loaded => string.Equals(loaded.GetName().Name, assemblyName, StringComparison.Ordinal));
                    Assembly assembly = assemblyLoader.LoadFrom(candidate);
                    return new ResolvedAssembly(assembly, WasLoaded: !wasLoadedBeforePathProbe, candidate);
                }
                catch
                {
                    // Swallow — try next probing path
                }
            }
        }

        string probes = probingPaths.Count == 0 ? "<none>" : string.Join(", ", probingPaths);
        throw new InvalidOperationException(
            $"Failed to resolve target assembly '{assemblyName}' from architecture YAML. Probing paths: {probes}");
    }

    private static string[] ResolveProbingPaths(
        ArchitectureContractDocument document,
        string? repositoryRoot,
        IArchitectureFileSystem fileSystem,
        IArchitectureEnvironment environment,
        IReadOnlyCollection<string>? additionalProbingPaths,
        IEnumerable<string> sharedFrameworkProbingPaths)
    {
        List<string> result = new();

        result.AddRange(ResolveEnvProbingPaths(fileSystem, environment));
        result.AddRange(ResolveConfiguredSearchPaths(document, repositoryRoot, fileSystem));
        result.AddRange(additionalProbingPaths ?? Array.Empty<string>());

        // An opted-in shared framework is one coherent runtime closure. It must win over the
        // tool's own app base, which can carry older Microsoft.Extensions.* packages; mixing
        // those with Microsoft.AspNetCore.App creates impossible interface implementations
        // such as WebApplicationBuilder.get_Logging on .NET 10. Consumer outputs above still
        // take precedence, so an application keeps its explicit package dependencies.
        result.AddRange(sharedFrameworkProbingPaths);

        string appBaseDirectory = environment.BaseDirectory;
        if (fileSystem.DirectoryExists(appBaseDirectory))
        {
            result.Add(appBaseDirectory);
        }

        AddRepositoryRootProbingPaths(repositoryRoot, fileSystem, result);

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ResolveConfiguredSearchPaths(
        ArchitectureContractDocument document,
        string? repositoryRoot,
        IArchitectureFileSystem fileSystem)
    {
        foreach (string rawPath in document.Analysis.AssemblySearchPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string resolvedPath = ResolveConfiguredSearchPath(rawPath.Trim(), repositoryRoot);
            if (!fileSystem.DirectoryExists(resolvedPath))
            {
                continue;
            }

            yield return resolvedPath;
        }
    }

    private static string ResolveConfiguredSearchPath(string normalizedPath, string? repositoryRoot)
    {
        if (Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        return string.IsNullOrWhiteSpace(repositoryRoot)
            ? normalizedPath
            : Path.GetFullPath(Path.Combine(repositoryRoot, normalizedPath));
    }

    private static void AddRepositoryRootProbingPaths(
        string? repositoryRoot, IArchitectureFileSystem fileSystem, List<string> result)
    {
        if (!string.IsNullOrWhiteSpace(repositoryRoot) && fileSystem.DirectoryExists(repositoryRoot))
        {
            result.Add(repositoryRoot);
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return;
        }

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

    // Derives the target framework(s) actually selected for this run's target assemblies from their
    // resolved build output paths (".../bin/{configuration}/{targetFramework}/[runtime/]assembly.dll"),
    // rather than the ArchLinterNet CLI's own runtime — see ArchitectureSharedFrameworkResolver's
    // anchor-priority note for why the CLI's runtime major is not a safe substitute for this.
    private static IReadOnlyCollection<string> ExtractDiscoveredTargetFrameworks(
        IReadOnlyDictionary<string, string>? resolvedAssemblyPaths, IReadOnlyCollection<string> targetAssemblyNames)
    {
        if (resolvedAssemblyPaths is null || resolvedAssemblyPaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        HashSet<string> frameworks = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in targetAssemblyNames)
        {
            if (!resolvedAssemblyPaths.TryGetValue(name, out string? path))
            {
                continue;
            }

            string? framework = ExtractTargetFrameworkFromBuildOutputPath(path);
            if (!string.IsNullOrWhiteSpace(framework))
            {
                frameworks.Add(framework);
            }
        }

        return frameworks;
    }

    private static string? GetAssemblyLocation(Assembly assembly)
    {
        try
        {
            return string.IsNullOrEmpty(assembly.Location) ? null : Path.GetFullPath(assembly.Location);
        }
        catch (NotSupportedException)
        {
            return null;
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
    IReadOnlyCollection<string> AssemblyProbingPaths,
    int AssemblyLoads = 0,
    IArchitectureAssemblyLoadScope? IsolatedLoadScope = null)
{
    internal IReadOnlyCollection<string> SelectedAssemblyArtifactPaths { get; init; } = Array.Empty<string>();

    internal IReadOnlyDictionary<Assembly, string> ResolvedAssemblyArtifactPaths { get; init; } =
        new Dictionary<Assembly, string>();
}

internal sealed record ResolvedAssembly(Assembly Assembly, bool WasLoaded, string? ArtifactPath);
