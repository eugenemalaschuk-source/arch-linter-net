using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

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
    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly IReadOnlyList<string>? _preprocessorSymbols;
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IReadOnlyDictionary<string, string> _sourcePathAssemblyOwnership;
    private readonly Lazy<FactIndexData> _data;

    public ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null)
        : this(
            targetAssemblies,
            repositoryRoot,
            sourceRoots,
            preprocessorSymbols,
            fileSystem,
            projectDiscovery: null,
            sourceRootAssemblyOwnership: null)
    {
    }

    internal ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols,
        IArchitectureFileSystem? fileSystem,
        ProjectDiscoveryResult? projectDiscovery,
        IReadOnlyDictionary<string, string>? sourceRootAssemblyOwnership)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        _sourceRoots = sourceRoots ?? throw new ArgumentNullException(nameof(sourceRoots));
        _preprocessorSymbols = preprocessorSymbols;
        _fileSystem = fileSystem ?? ArchitectureFileSystem.Real;
        _sourcePathAssemblyOwnership = BuildSourcePathAssemblyOwnership(
            _targetAssemblies,
            _sourceRoots,
            projectDiscovery,
            sourceRootAssemblyOwnership);
        _data = new Lazy<FactIndexData>(BuildData);
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts => _data.Value.AllFacts;

    public IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities => _data.Value.Ambiguities;

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
            new SourceFactKey(assemblyName, fullTypeName),
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
        List<Assembly> sortedAssemblies = _targetAssemblies
            .Distinct()
            .OrderBy(a => a.GetName().Name ?? string.Empty, _ordinal)
            .ToList();

        Dictionary<string, List<BaseFact>> reflectionFacts = RunReflectionPass(sortedAssemblies);

        Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap =
            _sourceRoots.Count > 0 ? RunSourceScan() : [];

        (Dictionary<SourceFactKey, SourceInfo> resolvedSourceInfo,
            List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities) =
                ResolveSourceInfo(sourceMap);

        List<ArchitectureDeclaredTypeFact> allFacts = BuildFacts(reflectionFacts, resolvedSourceInfo);
        SortFactsAndAmbiguities(allFacts, ambiguities);
        return BuildFactIndexData(allFacts, ambiguities);
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
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities)
    {
        Dictionary<string, ArchitectureDeclaredTypeFact> uniqueFactsByName = new(_ordinal);
        Dictionary<SourceFactKey, ArchitectureDeclaredTypeFact> factsByAssemblyAndName = new();
        HashSet<string> ambiguousFullTypeNames = new(_ordinal);

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            if (!uniqueFactsByName.TryAdd(fact.FullTypeName, fact))
            {
                ambiguousFullTypeNames.Add(fact.FullTypeName);
            }

            factsByAssemblyAndName[new SourceFactKey(fact.AssemblyName, fact.FullTypeName)] = fact;
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

    // Step 1: walk every loadable type in each assembly and collect one BaseFact per
    // (assemblyName, fullTypeName). Assemblies are already sorted alphabetically before this call.
    private static Dictionary<string, List<BaseFact>> RunReflectionPass(List<Assembly> sortedAssemblies)
    {
        Dictionary<string, List<BaseFact>> factsByName = new(_ordinal);
        foreach (Assembly assembly in sortedAssemblies)
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
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
                    GetTypeKindFromReflection(type)));
            }
        }

        return factsByName;
    }

    // Step 2: parse every *.cs file under each configured source root and map
    // (assemblyName, fullTypeName) → [(file, kind)]. Preprocessor symbols are forwarded so
    // conditional declarations match the compiled assembly. Each file is correlated only when its
    // owning assembly can be determined from the most specific known project subtree.
    private Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> RunSourceScan()
    {
        Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap = [];
        List<(string SourceRoot, string AssemblyName)> ownershipEntries = _sourcePathAssemblyOwnership
            .Select(static kvp => (kvp.Key, kvp.Value))
            .ToList();

        foreach (string sourceRoot in _sourceRoots)
        {
            string normalizedSourceRoot = NormalizeRelativePath(sourceRoot);
            string absoluteRoot = Path.Combine(_repositoryRoot, normalizedSourceRoot);
            if (!_fileSystem.DirectoryExists(absoluteRoot)) continue;

            foreach (string absoluteFile in _fileSystem.EnumerateFiles(
                absoluteRoot,
                "*.cs",
                SearchOption.AllDirectories))
            {
                string normalizedFilePath = NormalizePath(_repositoryRoot, absoluteFile);
                string? assemblyName = ResolveOwnedAssemblyName(normalizedFilePath, ownershipEntries);
                if (assemblyName == null)
                {
                    continue;
                }

                ProcessSourceFile(sourceMap, assemblyName, absoluteRoot, absoluteFile);
            }
        }

        return sourceMap;
    }

    // Step 3: for each owned (assemblyName, CLR name), resolve it to either one source file
    // (enriched) or an ambiguity (partial class across multiple files).
    private static (
        Dictionary<SourceFactKey, SourceInfo> Resolved,
        List<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities)
        ResolveSourceInfo(
            Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap)
    {
        Dictionary<SourceFactKey, SourceInfo> resolved = [];
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = [];

        foreach (KeyValuePair<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> entry in sourceMap)
        {
            SourceFactKey key = entry.Key;

            // Deduplicate by path: overlapping roots or multiple declarations in one file are NOT ambiguous.
            List<string> uniquePaths = entry.Value
                .Select(e => e.FilePath)
                .Distinct(_ordinal)
                .OrderBy(p => p, _ordinal)
                .ToList();

            if (uniquePaths.Count == 1)
            {
                string relPath = uniquePaths[0];
                ArchitectureTypeKind kind = entry.Value.First(e => e.FilePath == relPath).Kind;
                resolved[key] = new SourceInfo(relPath, kind, IsAmbiguous: false);
            }
            else if (uniquePaths.Count > 1)
            {
                ambiguities.Add(new ArchitectureDeclaredTypeSourceAmbiguity(
                    key.AssemblyName,
                    key.FullTypeName,
                    uniquePaths));
                resolved[key] = new SourceInfo(null, ArchitectureTypeKind.Unknown, IsAmbiguous: true);
            }
        }

        return (resolved, ambiguities);
    }

    // Step 4: emit one ArchitectureDeclaredTypeFact per (assemblyName, fullTypeName) pair,
    // applying source enrichment where available.
    private static List<ArchitectureDeclaredTypeFact> BuildFacts(
        Dictionary<string, List<BaseFact>> reflectionFactsByName,
        Dictionary<SourceFactKey, SourceInfo> resolvedSourceInfo)
    {
        List<ArchitectureDeclaredTypeFact> allFacts = [];

        foreach (KeyValuePair<string, List<BaseFact>> entry in reflectionFactsByName
            .OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string fullName = entry.Key;

            foreach (BaseFact bf in entry.Value)
            {
                resolvedSourceInfo.TryGetValue(
                    new SourceFactKey(bf.AssemblyName, fullName),
                    out SourceInfo? sourceInfo);
                allFacts.Add(CreateFact(bf, fullName, sourceInfo));
            }
        }

        return allFacts;
    }

    private void ProcessSourceFile(
        Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap,
        string assemblyName,
        string absoluteRoot,
        string absoluteFile)
    {
        // Relative to the scanned root so ancestor directory names outside the repo
        // can never be mistaken for excluded segments.
        string relativeToRoot = Path.GetRelativePath(absoluteRoot, absoluteFile)
            .Replace('\\', '/');

        if (ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot)) return;
        if (!TryReadSourceText(absoluteFile, out string sourceText)) return;

        string normalizedFilePath = NormalizePath(_repositoryRoot, absoluteFile);
        AddParsedTypes(sourceMap, assemblyName, normalizedFilePath, sourceText);
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
        Dictionary<SourceFactKey, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap,
        string assemblyName,
        string normalizedFilePath,
        string sourceText)
    {
        foreach (ArchitectureDeclaredTypeParser.ParsedTypeInfo parsed in
            ArchitectureDeclaredTypeParser.ParseSourceText(sourceText, _preprocessorSymbols))
        {
            SourceFactKey key = new(assemblyName, parsed.FullTypeName);
            if (!sourceMap.TryGetValue(key, out List<(string, ArchitectureTypeKind)>? entries))
            {
                entries = [];
                sourceMap[key] = entries;
            }

            entries.Add((normalizedFilePath, parsed.TypeKind));
        }
    }

    private static Dictionary<string, string> BuildSourcePathAssemblyOwnership(
        IReadOnlyCollection<Assembly> targetAssemblies,
        IReadOnlyList<string> sourceRoots,
        ProjectDiscoveryResult? projectDiscovery,
        IReadOnlyDictionary<string, string>? explicitOwnership)
    {
        Dictionary<string, string> ownership = new(_ordinal);
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

                ownership[NormalizeRelativePath(sourcePath)] = assemblyName;
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
                    ownership[sourceRoot] = soleAssemblyName;
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
                ownership[discoveredRoot] = assemblyName;
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

    private static ArchitectureDeclaredTypeFact CreateFact(
        BaseFact baseFact,
        string fullName,
        SourceInfo? sourceInfo)
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
            null,
            null,
            [],
            namespaceSegments);
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
            // Delegates are sealed classes that inherit from MulticastDelegate (which itself inherits
            // from Delegate). Checking the base type avoids accidentally classifying MulticastDelegate
            // itself as a Delegate kind.
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

    private readonly record struct SourceFactKey(string AssemblyName, string FullTypeName);

    private sealed record BaseFact(
        string AssemblyName,
        string Namespace,
        string FullTypeName,
        string SimpleTypeName,
        ArchitectureTypeKind TypeKind);

    private sealed record SourceInfo(
        string? FilePath,
        ArchitectureTypeKind KindFromSource,
        bool IsAmbiguous);

    private sealed record FactIndexData(
        Dictionary<string, ArchitectureDeclaredTypeFact> UniqueFactsByName,
        Dictionary<SourceFactKey, ArchitectureDeclaredTypeFact> FactsByAssemblyAndName,
        IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts,
        IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByFile,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByNamespace);
}
