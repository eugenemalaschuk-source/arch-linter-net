using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Owns the bounded, read-only reflection and source traversal used to materialize
// ArchitectureSourceFileFactIndex. The index remains the single lazy/cached facade; this
// collaborator only gathers traversal facts and consumed source inputs for one materialization.
internal sealed class ArchitectureSourceFileFactTraversal
{
    private static readonly StringComparer _ordinal = StringComparer.Ordinal;

    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly IReadOnlyList<string>? _preprocessorSymbols;
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IReadOnlyList<(string SourcePath, string AssemblyName)> _sourcePathAssemblyOwnership;
    private readonly CancellationToken _cancellationToken;
    private readonly AnalysisSessionProfilingCounters? _profilingCounters;
    private readonly int _maxParallelism;
    private readonly int _parallelEligibilityThreshold;
    private readonly IBoundedParallelPartitionRunner _partitionRunner;

    internal ArchitectureSourceFileFactTraversal(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols,
        IArchitectureFileSystem fileSystem,
        ArchitectureSourceFileFactIndex.ProjectOwnership projectOwnership,
        ArchitectureSourceFileFactIndex.ConstructionOptions options)
    {
        _repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        _sourceRoots = sourceRoots ?? throw new ArgumentNullException(nameof(sourceRoots));
        _preprocessorSymbols = preprocessorSymbols;
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _sourcePathAssemblyOwnership = BuildSourcePathAssemblyOwnership(
            targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies)),
            _sourceRoots,
            projectOwnership.ProjectDiscovery,
            projectOwnership.SourceRootAssemblyOwnership);
        _cancellationToken = options.CancellationToken;
        _profilingCounters = options.ProfilingCounters;
        _maxParallelism = options.MaxParallelism;
        _parallelEligibilityThreshold =
            options.ParallelEligibilityThresholdOverride ?? BoundedParallelPartitionRunner.DefaultParallelEligibilityThreshold;
        _partitionRunner = options.PartitionRunner ?? new BoundedParallelPartitionRunner();
    }

    // Step 1: walk every loadable type in each assembly and collect one BaseFact per
    // (assemblyName, fullTypeName). Assemblies are already sorted alphabetically before this call.
    // Bounded-parallel across assemblies: each assembly's reflection pass is independent, and the
    // runner returns partition slots in input order so the merge never depends on completion order.
    internal Dictionary<string, List<BaseFact>> RunReflectionPass(List<Assembly> sortedAssemblies)
    {
        Dictionary<string, List<BaseFact>>[] perAssemblyFacts = _partitionRunner.Run(
            sortedAssemblies,
            _maxParallelism,
            (assembly, _) => BuildReflectionFactsForAssembly(assembly),
            _cancellationToken,
            _profilingCounters,
            _parallelEligibilityThreshold);

        _cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, List<BaseFact>> factsByName = new(_ordinal);
        foreach (Dictionary<string, List<BaseFact>> assemblyFacts in perAssemblyFacts)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            foreach (KeyValuePair<string, List<BaseFact>> entry in assemblyFacts)
            {
                if (!factsByName.TryGetValue(entry.Key, out List<BaseFact>? list))
                {
                    list = [];
                    factsByName[entry.Key] = list;
                }

                list.AddRange(entry.Value);
            }
        }

        return factsByName;
    }

    internal SourceScanResult RunSourceScan()
    {
        _profilingCounters?.RecordSourceScanPass();
        IReadOnlyList<(string SourceRoot, string AssemblyName)> ownershipEntries = _sourcePathAssemblyOwnership
            .Select(static entry => (entry.SourcePath, entry.AssemblyName))
            .ToList();

        SourceScanResult[] perRootResults = _partitionRunner.Run(
            _sourceRoots,
            _maxParallelism,
            (sourceRoot, _) => ScanSourceRoot(sourceRoot, ownershipEntries),
            _cancellationToken,
            _profilingCounters,
            _parallelEligibilityThreshold);

        _cancellationToken.ThrowIfCancellationRequested();

        Dictionary<SourceFactKey, List<SourceDeclaration>> sourceMap = [];
        List<string> consumedSourceInputPaths = [];
        foreach (SourceScanResult rootResult in perRootResults)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            foreach (KeyValuePair<SourceFactKey, List<SourceDeclaration>> entry in rootResult.SourceMap)
            {
                if (!sourceMap.TryGetValue(entry.Key, out List<SourceDeclaration>? entries))
                {
                    entries = [];
                    sourceMap[entry.Key] = entries;
                }

                entries.AddRange(entry.Value);
            }

            consumedSourceInputPaths.AddRange(rootResult.ConsumedSourceInputPaths);
        }

        return new SourceScanResult(
            sourceMap,
            consumedSourceInputPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, _ordinal)
                .ToArray());
    }

    private Dictionary<string, List<BaseFact>> BuildReflectionFactsForAssembly(Assembly assembly)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, List<BaseFact>> factsByName = new(_ordinal);
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly, _cancellationToken))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            string? fullName = SafeFullName(type);
            if (string.IsNullOrEmpty(fullName)) continue;

            string ns = SafeNamespace(type);
            if (!factsByName.TryGetValue(fullName, out List<BaseFact>? list))
            {
                list = [];
                factsByName[fullName] = list;
            }

            list.Add(new BaseFact(
                assemblyName,
                ns,
                fullName,
                GetSimpleTypeName(type),
                GetTypeKindFromReflection(type),
                type.IsAbstract));
        }

        return factsByName;
    }

    private SourceScanResult ScanSourceRoot(
        string sourceRoot,
        IReadOnlyList<(string SourceRoot, string AssemblyName)> ownershipEntries)
    {
        Dictionary<SourceFactKey, List<SourceDeclaration>> localMap = [];
        List<string> consumedSourceInputPaths = [];
        _cancellationToken.ThrowIfCancellationRequested();
        string normalizedSourceRoot = NormalizeRelativePath(sourceRoot);
        string absoluteRoot = Path.Combine(_repositoryRoot, normalizedSourceRoot);
        if (!_fileSystem.DirectoryExists(absoluteRoot))
        {
            return new SourceScanResult(localMap, consumedSourceInputPaths);
        }

        foreach (string absoluteFile in _fileSystem.EnumerateFiles(
            absoluteRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            string relativeToRoot = Path.GetRelativePath(absoluteRoot, absoluteFile)
                .Replace('\\', '/');

            // A file-system implementation must not be able to smuggle a file outside the
            // configured root into source correlation or the consumed-input manifest. This also
            // protects generated-file filtering from interpreting an ancestor path as a normal
            // relative source path.
            if (IsOutsideConfiguredSourceRoot(relativeToRoot))
            {
                continue;
            }

            string normalizedFilePath = NormalizePath(_repositoryRoot, absoluteFile);
            string? assemblyName = ResolveOwnedAssemblyName(normalizedFilePath, ownershipEntries);
            if (assemblyName == null)
            {
                continue;
            }

            if (ProcessSourceFile(localMap, assemblyName, absoluteRoot, absoluteFile))
            {
                consumedSourceInputPaths.Add(Path.GetFullPath(absoluteFile));
            }
        }

        return new SourceScanResult(localMap, consumedSourceInputPaths);
    }

    private bool ProcessSourceFile(
        Dictionary<SourceFactKey, List<SourceDeclaration>> sourceMap,
        string assemblyName,
        string absoluteRoot,
        string absoluteFile)
    {
        // Relative to the scanned root so ancestor directory names outside the repo
        // can never be mistaken for excluded segments.
        string relativeToRoot = Path.GetRelativePath(absoluteRoot, absoluteFile)
            .Replace('\\', '/');

        if (IsOutsideConfiguredSourceRoot(relativeToRoot)
            || ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot))
        {
            return false;
        }

        if (!TryReadSourceText(absoluteFile, out string sourceText)) return false;

        // Count only files that passed generated-file exclusion and were successfully read,
        // i.e. the files the parser actually receives.
        _profilingCounters?.RecordSourceFileScanned();

        string normalizedFilePath = NormalizePath(_repositoryRoot, absoluteFile);
        AddParsedTypes(sourceMap, assemblyName, normalizedFilePath, sourceText);
        return true;
    }

    private bool TryReadSourceText(string absoluteFile, out string sourceText)
    {
        try
        {
            sourceText = _fileSystem.ReadAllText(absoluteFile);
            return true;
        }
        catch (IOException)
        {
            sourceText = string.Empty;
            return false;
        }
    }

    private void AddParsedTypes(
        Dictionary<SourceFactKey, List<SourceDeclaration>> sourceMap,
        string assemblyName,
        string normalizedFilePath,
        string sourceText)
    {
        foreach (ArchitectureDeclaredTypeParser.ParsedTypeInfo parsed in
            ArchitectureDeclaredTypeParser.ParseSourceText(sourceText, _preprocessorSymbols))
        {
            SourceFactKey key = new(assemblyName, parsed.FullTypeName);
            if (!sourceMap.TryGetValue(key, out List<SourceDeclaration>? entries))
            {
                entries = [];
                sourceMap[key] = entries;
            }

            entries.Add(new SourceDeclaration(
                normalizedFilePath,
                parsed.TypeKind,
                parsed.IsPartial,
                parsed.IsAbstract,
                parsed.SourceLine));
        }
    }

    private static List<(string SourcePath, string AssemblyName)> BuildSourcePathAssemblyOwnership(
        IReadOnlyCollection<Assembly> targetAssemblies,
        IReadOnlyList<string> sourceRoots,
        ProjectDiscoveryResult? projectDiscovery,
        IReadOnlyDictionary<string, string>? explicitOwnership)
    {
        List<(string SourcePath, string AssemblyName)> ownership = [];
        HashSet<string> targetAssemblyNames = targetAssemblies
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .ToHashSet(_ordinal);

        if (explicitOwnership != null)
        {
            foreach ((string sourcePath, string assemblyName) in explicitOwnership)
            {
                if (!targetAssemblyNames.Contains(assemblyName))
                {
                    continue;
                }

                ownership.Add((NormalizeRelativePath(sourcePath), assemblyName));
            }

            return ownership;
        }

        if (projectDiscovery == null)
        {
            if (targetAssemblyNames.Count == 1)
            {
                string soleAssemblyName = targetAssemblyNames.First();
                foreach (string sourceRoot in sourceRoots
                             .Select(NormalizeRelativePath)
                             .Distinct(_ordinal))
                {
                    ownership.Add((sourceRoot, soleAssemblyName));
                }
            }

            return ownership;
        }

        List<(string SourceRoot, string AssemblyName)> discoveredRoots = projectDiscovery.DiscoveredProjects
            .Where(project => targetAssemblyNames.Contains(project.AssemblyName))
            .Select(project => (NormalizeRelativePath(GetProjectDirectory(project.Path)), project.AssemblyName))
            .ToList();

        foreach ((string discoveredRoot, string assemblyName) in discoveredRoots)
        {
            if (sourceRoots
                .Select(NormalizeRelativePath)
                .Distinct(_ordinal)
                .Any(configuredRoot => PathsOverlap(discoveredRoot, configuredRoot)))
            {
                ownership.Add((discoveredRoot, assemblyName));
            }
        }

        return ownership;
    }

    private static string? ResolveOwnedAssemblyName(
        string sourceRoot,
        IReadOnlyList<(string SourceRoot, string AssemblyName)> discoveredRoots)
    {
        List<(string SourceRoot, string AssemblyName)> exactMatches = discoveredRoots
            .Where(entry => _ordinal.Equals(entry.SourceRoot, sourceRoot))
            .ToList();

        if (exactMatches.Count == 1)
        {
            return exactMatches[0].AssemblyName;
        }

        if (exactMatches.Count > 1)
        {
            return null;
        }

        List<(string SourceRoot, string AssemblyName)> ancestorMatches = discoveredRoots
            .Where(entry => IsSameOrDescendantPath(sourceRoot, entry.SourceRoot))
            .OrderByDescending(entry => entry.SourceRoot.Length)
            .ToList();

        if (ancestorMatches.Count == 0)
        {
            return null;
        }

        int longestLength = ancestorMatches[0].SourceRoot.Length;
        List<string> mostSpecificAssemblies = ancestorMatches
            .Where(entry => entry.SourceRoot.Length == longestLength)
            .Select(entry => entry.AssemblyName)
            .Distinct(_ordinal)
            .ToList();

        return mostSpecificAssemblies.Count == 1 ? mostSpecificAssemblies[0] : null;
    }

    private static bool IsSameOrDescendantPath(string path, string ancestor)
    {
        if (ancestor == ".")
        {
            return true;
        }

        return _ordinal.Equals(path, ancestor)
            || (path.Length > ancestor.Length
                && path.StartsWith(ancestor, StringComparison.Ordinal)
                && path[ancestor.Length] == '/');
    }

    private static bool PathsOverlap(string left, string right)
    {
        return IsSameOrDescendantPath(left, right) || IsSameOrDescendantPath(right, left);
    }

    private static string GetProjectDirectory(string projectPath)
    {
        string normalizedProjectPath = NormalizeRelativePath(projectPath);
        int slash = normalizedProjectPath.LastIndexOf('/');
        return slash >= 0 ? normalizedProjectPath[..slash] : ".";
    }

    private static bool IsOutsideConfiguredSourceRoot(string relativePath)
    {
        return relativePath.Length == 0
            || relativePath == "."
            || relativePath == ".."
            || relativePath.StartsWith("../", StringComparison.Ordinal);
    }

    private static string NormalizePath(string repositoryRoot, string absoluteFilePath)
    {
        try
        {
            return NormalizeRelativePath(Path.GetRelativePath(repositoryRoot, absoluteFilePath));
        }
        catch (Exception)
        {
            return NormalizeRelativePath(absoluteFilePath);
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        normalized = normalized.TrimEnd('/');
        return normalized.Length == 0 ? "." : normalized;
    }

    private static string GetSimpleTypeName(Type type)
    {
        string name = type.Name;
        int backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    private static string? SafeFullName(Type type)
    {
        try { return type.FullName; }
        catch { return null; }
    }

    private static string SafeNamespace(Type type)
    {
        try { return type.Namespace ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static ArchitectureTypeKind GetTypeKindFromReflection(Type type)
    {
        if (type.IsEnum) return ArchitectureTypeKind.Enum;
        if (type.IsValueType) return ArchitectureTypeKind.Struct;
        if (type.IsInterface) return ArchitectureTypeKind.Interface;
        if (type.IsClass)
        {
            // Delegates are sealed classes that inherit from MulticastDelegate. Checking the base
            // type avoids classifying MulticastDelegate itself as a Delegate kind.
            if (type.BaseType != null &&
                typeof(MulticastDelegate).IsAssignableFrom(type) &&
                type != typeof(MulticastDelegate) &&
                type != typeof(Delegate))
            {
                return ArchitectureTypeKind.Delegate;
            }

            return ArchitectureTypeKind.Class;
        }

        return ArchitectureTypeKind.Unknown;
    }

    internal readonly record struct SourceFactKey(string AssemblyName, string FullTypeName);

    internal sealed record BaseFact(
        string AssemblyName,
        string Namespace,
        string FullTypeName,
        string SimpleTypeName,
        ArchitectureTypeKind TypeKind,
        bool IsAbstract);

    internal sealed record SourceInfo(
        string? FilePath,
        ArchitectureTypeKind KindFromSource,
        bool IsAbstract,
        bool IsAmbiguous);

    internal sealed record SourceDeclaration(
        string FilePath,
        ArchitectureTypeKind Kind,
        bool IsPartial,
        bool IsAbstract,
        int SourceLine);

    internal sealed record SourceScanResult(
        Dictionary<SourceFactKey, List<SourceDeclaration>> SourceMap,
        IReadOnlyList<string> ConsumedSourceInputPaths)
    {
        internal static SourceScanResult Empty { get; } = new([], Array.Empty<string>());
    }
}
