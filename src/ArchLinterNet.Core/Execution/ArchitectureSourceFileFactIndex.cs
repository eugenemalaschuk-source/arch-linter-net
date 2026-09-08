using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Per-run, lazily-computed index of declared-type facts built by correlating CLR reflection
// metadata with Roslyn syntax-only source file parsing. Follows the same Lazy<T>-on-first-access
// pattern as ArchitectureTypeIndex and ArchitectureRoleIndex: constructed eagerly in
// ArchitectureAnalysisSession but never touches the filesystem or assemblies until first accessed.
//
// Key design decisions (see design.md for full rationale):
// - Reflection-first: every loadable type gets a fact; source enriches path/kind when available.
// - Assembly-aware identity: one fact per (assemblyName, fullTypeName) pair. The same CLR full
//   name in multiple assemblies produces separate facts (all in AllFacts). TryGetFact(string)
//   returns false for ambiguous names; TryGetFact(assemblyName, fullTypeName) is exact.
// - Source correlation is assembly-aware too: each scanned file contributes declarations only when
//   its owning assembly can be determined. For standalone single-target runs without project
//   discovery, all configured source roots are owned by that sole target assembly; otherwise
//   unowned files are ignored rather than guessed.
// - CLR-format full names (dots, +, `N) used as index keys throughout.
// - Empty sourceRoots → reflection-only facts (null SourceFilePath) with no filesystem access.
// - Ambiguity: same owned CLR name declared in more than one distinct file (partial class across
//   files). A single file referenced twice (e.g. via overlapping source roots) is NOT an ambiguity.
// - Partial classes across files → ArchitectureDeclaredTypeSourceAmbiguity + null SourceFilePath.
// - Record detection requires Roslyn source analysis; reflection falls back to Class/Struct.
// - Paths normalized to forward slashes, relative to repositoryRoot.
// - All public collections are returned in deterministic (ordinal-sorted) order.
public sealed class ArchitectureSourceFileFactIndex
{
    private static readonly StringComparer _ordinal = StringComparer.Ordinal;

    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly CancellationToken _cancellationToken;
    private readonly AnalysisSessionProfilingCounters? _profilingCounters;
    private readonly ArchitectureSourceFileFactTraversal _traversal;
    private readonly Lazy<FactIndexData> _data;

    public ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null,
        CancellationToken cancellationToken = default)
        : this(
            targetAssemblies,
            repositoryRoot,
            sourceRoots,
            preprocessorSymbols,
            fileSystem,
            default,
            new ConstructionOptions(ProfilingCounters: null, CancellationToken: cancellationToken))
    {
    }

    // Bundles the two "how source roots map to project-owning assemblies" inputs — always
    // supplied (or omitted) together — so the internal constructor below stays under the
    // parameter-count limit rather than taking each as its own parameter.
    internal readonly record struct ProjectOwnership(
        ProjectDiscoveryResult? ProjectDiscovery,
        IReadOnlyDictionary<string, string>? SourceRootAssemblyOwnership);

    internal readonly record struct ConstructionOptions(
        AnalysisSessionProfilingCounters? ProfilingCounters,
        CancellationToken CancellationToken,
        int MaxParallelism = 0,
        int? ParallelEligibilityThresholdOverride = null,
        IBoundedParallelPartitionRunner? PartitionRunner = null);

    internal ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols,
        IArchitectureFileSystem? fileSystem,
        ProjectOwnership projectOwnership,
        ConstructionOptions options = default)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _sourceRoots = sourceRoots ?? throw new ArgumentNullException(nameof(sourceRoots));
        _cancellationToken = options.CancellationToken;
        _profilingCounters = options.ProfilingCounters;
        _traversal = new ArchitectureSourceFileFactTraversal(
            _targetAssemblies,
            repositoryRoot,
            _sourceRoots,
            preprocessorSymbols,
            fileSystem ?? ArchitectureFileSystem.Real,
            projectOwnership,
            options);
        _data = new Lazy<FactIndexData>(BuildData);
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts => _data.Value.AllFacts;

    public IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities => _data.Value.Ambiguities;

    // Unlike AllFacts, this preserves every source declaration of a type, including every part
    // of a partial type. Consumers that need one unambiguous source path must keep using AllFacts.
    internal IReadOnlyList<ArchitectureTypeSourceDeclaration> SourceDeclarations => _data.Value.SourceDeclarations;

    // Do not force lazy source materialization merely to publish an input manifest. If a contract
    // consumed source text, BuildData retained the exact files successfully passed to the parser;
    // otherwise there are no source files to protect from this analysis session.
    internal IReadOnlyList<string> ConsumedSourceInputPaths => _data.IsValueCreated
        ? _data.Value.ConsumedSourceInputPaths
        : Array.Empty<string>();

    public bool TryGetFact(string fullTypeName, out ArchitectureDeclaredTypeFact fact)
    {
        ArgumentNullException.ThrowIfNull(fullTypeName);
        return _data.Value.UniqueFactsByName.TryGetValue(fullTypeName, out fact!);
    }

    // Assembly-aware overload: returns the fact for exactly (assemblyName, fullTypeName).
    // Use this when the caller already knows which assembly it cares about — e.g. a path/layout
    // rule that receives a Type instance and can supply Type.Assembly.GetName().Name directly.
    // Returns false when no type with that name was found in that assembly.
    public bool TryGetFact(string assemblyName, string fullTypeName, out ArchitectureDeclaredTypeFact fact)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(fullTypeName);
        return _data.Value.FactsByAssemblyAndName.TryGetValue(
            new ArchitectureSourceFileFactTraversal.SourceFactKey(assemblyName, fullTypeName),
            out fact!);
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> GetFactsForFile(string relativeFilePath)
    {
        ArgumentNullException.ThrowIfNull(relativeFilePath);
        string normalized = NormalizeRelativePath(relativeFilePath);
        return _data.Value.ByFile.TryGetValue(normalized, out IReadOnlyList<ArchitectureDeclaredTypeFact>? list)
            ? list
            : Array.Empty<ArchitectureDeclaredTypeFact>();
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> GetFactsForNamespace(string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        return _data.Value.ByNamespace.TryGetValue(namespaceName, out IReadOnlyList<ArchitectureDeclaredTypeFact>? list)
            ? list
            : Array.Empty<ArchitectureDeclaredTypeFact>();
    }

    private FactIndexData BuildData()
    {
        _profilingCounters?.RecordFactIndexMaterialization();
        _cancellationToken.ThrowIfCancellationRequested();

        List<Assembly> sortedAssemblies = _targetAssemblies
            .Distinct()
            .OrderBy(a => a.GetName().Name ?? string.Empty, _ordinal)
            .ToList();

        Dictionary<string, List<ArchitectureSourceFileFactTraversal.BaseFact>> reflectionFacts =
            _traversal.RunReflectionPass(sortedAssemblies);

        _cancellationToken.ThrowIfCancellationRequested();

        ArchitectureSourceFileFactTraversal.SourceScanResult sourceScan = _sourceRoots.Count > 0
            ? _traversal.RunSourceScan()
            : ArchitectureSourceFileFactTraversal.SourceScanResult.Empty;
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            List<ArchitectureSourceFileFactTraversal.SourceDeclaration>> sourceMap = sourceScan.SourceMap;

        (Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureSourceFileFactTraversal.SourceInfo> resolvedSourceInfo,
            List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities) =
                ResolveSourceInfo(sourceMap);

        List<ArchitectureDeclaredTypeFact> allFacts = BuildFacts(reflectionFacts, resolvedSourceInfo);
        SortFactsAndAmbiguities(allFacts, ambiguities);
        return BuildFactIndexData(
            allFacts,
            ambiguities,
            BuildSourceDeclarations(sourceMap),
            sourceScan.ConsumedSourceInputPaths);
    }

    private static void SortFactsAndAmbiguities(
        List<ArchitectureDeclaredTypeFact> allFacts,
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities)
    {
        allFacts.Sort((a, b) =>
        {
            int c = _ordinal.Compare(a.FullTypeName, b.FullTypeName);
            return c != 0 ? c : _ordinal.Compare(a.AssemblyName, b.AssemblyName);
        });
        ambiguities.Sort((a, b) =>
        {
            int c = _ordinal.Compare(a.FullTypeName, b.FullTypeName);
            return c != 0 ? c : _ordinal.Compare(a.AssemblyName, b.AssemblyName);
        });
    }

    private static FactIndexData BuildFactIndexData(
        List<ArchitectureDeclaredTypeFact> allFacts,
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities,
        IReadOnlyList<ArchitectureTypeSourceDeclaration> sourceDeclarations,
        IReadOnlyList<string> consumedSourceInputPaths)
    {
        Dictionary<string, ArchitectureDeclaredTypeFact> uniqueFactsByName = new(_ordinal);
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureDeclaredTypeFact> factsByAssemblyAndName = new();
        HashSet<string> ambiguousFullTypeNames = new(_ordinal);

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            if (!uniqueFactsByName.TryAdd(fact.FullTypeName, fact))
            {
                ambiguousFullTypeNames.Add(fact.FullTypeName);
            }

            factsByAssemblyAndName[new ArchitectureSourceFileFactTraversal.SourceFactKey(
                fact.AssemblyName, fact.FullTypeName)] = fact;
        }

        foreach (string ambiguousFullTypeName in ambiguousFullTypeNames)
        {
            uniqueFactsByName.Remove(ambiguousFullTypeName);
        }

        (Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> byFile,
            Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> byNamespace) =
                BuildFileAndNamespaceIndexes(allFacts);

        return new FactIndexData(
            uniqueFactsByName,
            factsByAssemblyAndName,
            allFacts,
            ambiguities,
            sourceDeclarations,
            consumedSourceInputPaths,
            byFile,
            byNamespace);
    }

    private static (
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByFile,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByNamespace)
        BuildFileAndNamespaceIndexes(IReadOnlyList<ArchitectureDeclaredTypeFact> allFacts)
    {
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byFile = new(_ordinal);
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byNamespace = new(_ordinal);

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            if (fact.SourceFilePath != null)
            {
                if (!byFile.TryGetValue(fact.SourceFilePath, out List<ArchitectureDeclaredTypeFact>? fl))
                {
                    fl = [];
                    byFile[fact.SourceFilePath] = fl;
                }

                fl.Add(fact);
            }

            if (!byNamespace.TryGetValue(fact.Namespace, out List<ArchitectureDeclaredTypeFact>? nl))
            {
                nl = [];
                byNamespace[fact.Namespace] = nl;
            }

            nl.Add(fact);
        }

        return (
            byFile.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ArchitectureDeclaredTypeFact>)kvp.Value,
                _ordinal),
            byNamespace.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ArchitectureDeclaredTypeFact>)kvp.Value,
                _ordinal));
    }

    private static ArchitectureTypeSourceDeclaration[] BuildSourceDeclarations(
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            List<ArchitectureSourceFileFactTraversal.SourceDeclaration>> sourceMap)
    {
        return sourceMap
            .SelectMany(entry => entry.Value.Select(declaration => new ArchitectureTypeSourceDeclaration(
                entry.Key.AssemblyName,
                entry.Key.FullTypeName,
                declaration.Kind,
                declaration.IsPartial,
                declaration.IsAbstract,
                declaration.FilePath,
                declaration.SourceLine)))
            // Overlapping source roots may scan the same declaration more than once; that is not a
            // second C# declaration and must not consume a type's declaration budget twice.
            .Distinct()
            .OrderBy(declaration => declaration.FullTypeName, _ordinal)
            .ThenBy(declaration => declaration.AssemblyName, _ordinal)
            .ThenBy(declaration => declaration.SourceFilePath, _ordinal)
            .ThenBy(declaration => declaration.SourceLine)
            .ToArray();
    }

    // Step 3: for each owned (assemblyName, CLR name), resolve it to either one source file
    // (enriched) or an ambiguity (partial class across multiple files).
    private static (
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureSourceFileFactTraversal.SourceInfo> Resolved,
        List<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities)
        ResolveSourceInfo(
            Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
                List<ArchitectureSourceFileFactTraversal.SourceDeclaration>> sourceMap)
    {
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureSourceFileFactTraversal.SourceInfo> resolved = [];
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = [];

        foreach (KeyValuePair<ArchitectureSourceFileFactTraversal.SourceFactKey,
            List<ArchitectureSourceFileFactTraversal.SourceDeclaration>> entry in sourceMap)
        {
            ArchitectureSourceFileFactTraversal.SourceFactKey key = entry.Key;

            // Deduplicate by path: overlapping roots or multiple declarations in one file are NOT ambiguous.
            List<string> uniquePaths = entry.Value
                .Select(e => e.FilePath)
                .Distinct(_ordinal)
                .OrderBy(p => p, _ordinal)
                .ToList();

            if (uniquePaths.Count == 1)
            {
                string relPath = uniquePaths[0];
                ArchitectureSourceFileFactTraversal.SourceDeclaration declaration =
                    entry.Value.First(e => e.FilePath == relPath);
                resolved[key] = new ArchitectureSourceFileFactTraversal.SourceInfo(
                    relPath,
                    declaration.Kind,
                    declaration.IsAbstract,
                    IsAmbiguous: false);
            }
            else if (uniquePaths.Count > 1)
            {
                ambiguities.Add(new ArchitectureDeclaredTypeSourceAmbiguity(
                    key.AssemblyName,
                    key.FullTypeName,
                    uniquePaths));
                resolved[key] = new ArchitectureSourceFileFactTraversal.SourceInfo(
                    null, ArchitectureTypeKind.Unknown, IsAbstract: false, IsAmbiguous: true);
            }
        }

        return (resolved, ambiguities);
    }

    // Step 4: emit one ArchitectureDeclaredTypeFact per (assemblyName, fullTypeName) pair,
    // applying source enrichment where available.
    private static List<ArchitectureDeclaredTypeFact> BuildFacts(
        Dictionary<string, List<ArchitectureSourceFileFactTraversal.BaseFact>> reflectionFactsByName,
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureSourceFileFactTraversal.SourceInfo> resolvedSourceInfo)
    {
        List<ArchitectureDeclaredTypeFact> allFacts = [];

        foreach (KeyValuePair<string, List<ArchitectureSourceFileFactTraversal.BaseFact>> entry in reflectionFactsByName
            .OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string fullName = entry.Key;

            foreach (ArchitectureSourceFileFactTraversal.BaseFact bf in entry.Value)
            {
                resolvedSourceInfo.TryGetValue(
                    new ArchitectureSourceFileFactTraversal.SourceFactKey(bf.AssemblyName, fullName),
                    out ArchitectureSourceFileFactTraversal.SourceInfo? sourceInfo);
                allFacts.Add(CreateFact(bf, fullName, sourceInfo));
            }
        }

        return allFacts;
    }

    private static ArchitectureDeclaredTypeFact CreateFact(
        ArchitectureSourceFileFactTraversal.BaseFact baseFact,
        string fullName,
        ArchitectureSourceFileFactTraversal.SourceInfo? sourceInfo)
    {
        string[] namespaceSegments = GetNamespaceSegments(baseFact.Namespace);

        if (sourceInfo is { IsAmbiguous: true })
        {
            return new ArchitectureDeclaredTypeFact(
                baseFact.AssemblyName,
                baseFact.Namespace,
                fullName,
                baseFact.SimpleTypeName,
                baseFact.TypeKind,
                baseFact.IsAbstract,
                null,
                null,
                [],
                namespaceSegments);
        }

        if (sourceInfo?.FilePath != null)
        {
            return new ArchitectureDeclaredTypeFact(
                baseFact.AssemblyName,
                baseFact.Namespace,
                fullName,
                baseFact.SimpleTypeName,
                sourceInfo.KindFromSource,
                sourceInfo.IsAbstract,
                sourceInfo.FilePath,
                GetFileNameWithoutExtension(sourceInfo.FilePath),
                GetFolderSegments(sourceInfo.FilePath),
                namespaceSegments);
        }

        return new ArchitectureDeclaredTypeFact(
            baseFact.AssemblyName,
            baseFact.Namespace,
            fullName,
            baseFact.SimpleTypeName,
            baseFact.TypeKind,
            baseFact.IsAbstract,
            null,
            null,
            [],
            namespaceSegments);
    }

    private static string NormalizeRelativePath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        normalized = normalized.TrimEnd('/');
        return normalized.Length == 0 ? "." : normalized;
    }

    private static string? GetFileNameWithoutExtension(string normalizedRelativePath)
    {
        int lastSlash = normalizedRelativePath.LastIndexOf('/');
        string fileName = lastSlash >= 0
            ? normalizedRelativePath[(lastSlash + 1)..]
            : normalizedRelativePath;

        int dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static string[] GetFolderSegments(string normalizedRelativePath)
    {
        int lastSlash = normalizedRelativePath.LastIndexOf('/');
        if (lastSlash <= 0) return [];
        string dir = normalizedRelativePath[..lastSlash];
        return dir.Split('/');
    }

    private static string[] GetNamespaceSegments(string ns) =>
        string.IsNullOrEmpty(ns) ? [] : ns.Split('.');

    private sealed record FactIndexData(
        Dictionary<string, ArchitectureDeclaredTypeFact> UniqueFactsByName,
        Dictionary<ArchitectureSourceFileFactTraversal.SourceFactKey,
            ArchitectureDeclaredTypeFact> FactsByAssemblyAndName,
        IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts,
        IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities,
        IReadOnlyList<ArchitectureTypeSourceDeclaration> SourceDeclarations,
        IReadOnlyList<string> ConsumedSourceInputPaths,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByFile,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByNamespace);

}
