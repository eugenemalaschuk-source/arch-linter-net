using System.Reflection;
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
//   returns the first-alphabetically assembly; TryGetFact(assemblyName, fullTypeName) is exact.
// - Source correlation is skipped when a CLR name appears in multiple assemblies: without
//   per-project source roots the index cannot determine which file belongs to which assembly,
//   so all affected facts get null SourceFilePath rather than a potentially wrong path.
// - CLR-format full names (dots, +, `N) used as index keys throughout.
// - Empty sourceRoots → reflection-only facts (null SourceFilePath) with no filesystem access.
// - Ambiguity: same CLR name declared in more than one distinct file (partial class across files).
//   A single file referenced twice (e.g. via overlapping source roots) is NOT an ambiguity.
// - Partial classes across files → ArchitectureDeclaredTypeSourceAmbiguity + null SourceFilePath.
// - Record detection requires Roslyn source analysis; reflection falls back to Class/Struct.
// - Paths normalized to forward slashes, relative to repositoryRoot.
// - All public collections are returned in deterministic (ordinal-sorted) order.
public sealed class ArchitectureSourceFileFactIndex
{
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly IReadOnlyList<string>? _preprocessorSymbols;
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly Lazy<FactIndexData> _data;

    public ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        _sourceRoots = sourceRoots ?? throw new ArgumentNullException(nameof(sourceRoots));
        _preprocessorSymbols = preprocessorSymbols;
        _fileSystem = fileSystem ?? ArchitectureFileSystem.Real;
        _data = new Lazy<FactIndexData>(BuildData);
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts => _data.Value.AllFacts;

    public IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities => _data.Value.Ambiguities;

    public bool TryGetFact(string fullTypeName, out ArchitectureDeclaredTypeFact fact)
    {
        ArgumentNullException.ThrowIfNull(fullTypeName);
        return _data.Value.FactsByName.TryGetValue(fullTypeName, out fact!);
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
            (assemblyName, fullTypeName), out fact!);
    }

    public IReadOnlyList<ArchitectureDeclaredTypeFact> GetFactsForFile(string relativeFilePath)
    {
        ArgumentNullException.ThrowIfNull(relativeFilePath);
        string normalized = relativeFilePath.Replace('\\', '/');
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
        // Sort assemblies alphabetically so output order is deterministic regardless of
        // the order in which assemblies were passed to the constructor.
        List<Assembly> sortedAssemblies = _targetAssemblies
            .Distinct()
            .OrderBy(a => a.GetName().Name ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        // Step 1 — reflection pass: accumulate one BaseFact per (assemblyName, fullTypeName).
        // Using List<BaseFact> per CLR fullName so cross-assembly name collisions keep ALL facts
        // rather than the last writer winning. AllFacts will contain one entry per pair.
        Dictionary<string, List<BaseFact>> reflectionFactsByName =
            new(StringComparer.Ordinal);

        foreach (Assembly assembly in sortedAssemblies)
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                string? fullName = SafeFullName(type);
                if (string.IsNullOrEmpty(fullName)) continue;

                string ns = SafeNamespace(type);
                if (!reflectionFactsByName.TryGetValue(fullName, out List<BaseFact>? list))
                {
                    list = new List<BaseFact>();
                    reflectionFactsByName[fullName] = list;
                }

                list.Add(new BaseFact(assemblyName, ns, fullName,
                    GetSimpleTypeName(type), GetTypeKindFromReflection(type)));
            }
        }

        // Identify CLR names that appear in more than one assembly. For these types, source
        // ownership is indeterminate (source roots are global, not per-project), so we must not
        // propagate a source path from one assembly to another, and multiple distinct source files
        // for such a name are NOT a partial-class ambiguity — they may simply be the separate
        // implementations in each project.
        HashSet<string> multiAssemblyNames = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<BaseFact>> kvp in reflectionFactsByName)
        {
            if (kvp.Value.Count > 1) multiAssemblyNames.Add(kvp.Key);
        }

        // Step 2 — source scan: parse each .cs file and collect FullName → file+kind entries.
        // Preprocessor symbols are forwarded so conditional declarations match the compiled assembly.
        Dictionary<string, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap =
            new(StringComparer.Ordinal);

        if (_sourceRoots.Count > 0)
        {
            foreach (string sourceRoot in _sourceRoots)
            {
                string absoluteRoot = Path.Combine(_repositoryRoot, sourceRoot);
                if (!_fileSystem.DirectoryExists(absoluteRoot)) continue;

                foreach (string absoluteFile in _fileSystem.EnumerateFiles(
                    absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    // Checked relative to the scanned root (not repo root) so ancestor directory
                    // names outside the repository can never be mistaken for excluded segments.
                    string relativeToRoot = Path.GetRelativePath(absoluteRoot, absoluteFile)
                        .Replace('\\', '/');

                    if (ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot)) continue;

                    string sourceText;
                    try { sourceText = _fileSystem.ReadAllText(absoluteFile); }
                    catch (IOException) { continue; }

                    string normalizedFilePath = NormalizePath(_repositoryRoot, absoluteFile);

                    foreach (ArchitectureDeclaredTypeParser.ParsedTypeInfo parsed in
                        ArchitectureDeclaredTypeParser.ParseSourceText(sourceText, _preprocessorSymbols))
                    {
                        if (!sourceMap.TryGetValue(parsed.FullTypeName, out List<(string, ArchitectureTypeKind)>? entries))
                        {
                            entries = new List<(string, ArchitectureTypeKind)>();
                            sourceMap[parsed.FullTypeName] = entries;
                        }

                        entries.Add((normalizedFilePath, parsed.TypeKind));
                    }
                }
            }
        }

        // Step 3 — merge: produce the final fact for every reflection-discovered type.
        //
        // Source information is absent for CLR names that appear in multiple assemblies (see the
        // multiAssemblyNames guard above). For all other names, resolvedSourceInfo carries either
        // a single file path (enriched fact) or an ambiguity marker (partial class across files).
        //
        // Ambiguity is determined by the count of DISTINCT normalized file paths (not raw entries)
        // for single-assembly CLR names only. This correctly handles: (a) overlapping source roots
        // that yield the same file twice, and (b) multiple declarations of the same type within
        // one file, both of which should not create an ambiguity.
        List<ArchitectureDeclaredTypeFact> allFacts = new();
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = new();

        // Pre-resolve source info per fullTypeName — only for single-assembly CLR names.
        // When the same CLR name is declared in multiple assemblies (multiAssemblyNames) we cannot
        // determine which source file belongs to which assembly without per-project source roots,
        // so we leave those entries absent (→ null SourceFilePath for every affected fact) rather
        // than silently assigning the wrong project's path to the wrong assembly's fact.
        // Multiple distinct source files for a multi-assembly CLR name are also NOT recorded as a
        // partial-class ambiguity — they are likely the separate implementations in each project.
        Dictionary<string, SourceInfo> resolvedSourceInfo = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<(string FilePath, ArchitectureTypeKind Kind)>> entry in sourceMap)
        {
            string fullName = entry.Key;

            // Skip source association for CLR names shared across assemblies.
            if (multiAssemblyNames.Contains(fullName)) continue;

            List<(string FilePath, ArchitectureTypeKind Kind)> sourceEntries = entry.Value;

            // Deduplicate by normalized path — one file appearing multiple times (overlapping roots
            // or multiple declarations in same file) is not an ambiguity.
            List<string> uniquePaths = sourceEntries
                .Select(e => e.FilePath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            if (uniquePaths.Count == 1)
            {
                string relPath = uniquePaths[0];
                ArchitectureTypeKind kindFromSource = sourceEntries.First(e => e.FilePath == relPath).Kind;
                resolvedSourceInfo[fullName] = new SourceInfo(relPath, kindFromSource, IsAmbiguous: false);
            }
            else if (uniquePaths.Count > 1)
            {
                ambiguities.Add(new ArchitectureDeclaredTypeSourceAmbiguity(fullName, uniquePaths));
                resolvedSourceInfo[fullName] = new SourceInfo(null, ArchitectureTypeKind.Unknown, IsAmbiguous: true);
            }
        }

        // Produce one fact per (assemblyName, fullTypeName) pair.
        foreach (KeyValuePair<string, List<BaseFact>> entry in reflectionFactsByName
            .OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string fullName = entry.Key;
            List<BaseFact> baseFacts = entry.Value; // already sorted by assembly name (assemblies sorted above)

            resolvedSourceInfo.TryGetValue(fullName, out SourceInfo? si);

            foreach (BaseFact baseFact in baseFacts)
            {
                ArchitectureDeclaredTypeFact fact;

                if (si is { IsAmbiguous: true })
                {
                    fact = new ArchitectureDeclaredTypeFact(
                        baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                        baseFact.TypeKind,
                        null, null, Array.Empty<string>(),
                        GetNamespaceSegments(baseFact.Namespace));
                }
                else if (si?.FilePath != null)
                {
                    fact = new ArchitectureDeclaredTypeFact(
                        baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                        si.KindFromSource,
                        si.FilePath,
                        GetFileNameWithoutExtension(si.FilePath),
                        GetFolderSegments(si.FilePath),
                        GetNamespaceSegments(baseFact.Namespace));
                }
                else
                {
                    fact = new ArchitectureDeclaredTypeFact(
                        baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                        baseFact.TypeKind,
                        null, null, Array.Empty<string>(),
                        GetNamespaceSegments(baseFact.Namespace));
                }

                allFacts.Add(fact);
            }
        }

        // Sort deterministically. Primary key: FullTypeName (ordinal); secondary: AssemblyName (ordinal).
        allFacts.Sort((a, b) =>
        {
            int c = StringComparer.Ordinal.Compare(a.FullTypeName, b.FullTypeName);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.AssemblyName, b.AssemblyName);
        });

        // Ambiguities are already deduplicated per fullTypeName; sort for determinism.
        ambiguities.Sort((a, b) => StringComparer.Ordinal.Compare(a.FullTypeName, b.FullTypeName));

        // Primary lookup by FullTypeName only: when the same fullTypeName appears in multiple
        // assemblies, the first alphabetically (guaranteed by allFacts sort) wins. Callers that
        // know the assembly should use TryGetFact(assemblyName, fullTypeName) instead.
        Dictionary<string, ArchitectureDeclaredTypeFact> factsByName =
            new(StringComparer.Ordinal);

        // Assembly-aware lookup: exact (assemblyName, fullTypeName) → fact.
        Dictionary<(string AssemblyName, string FullTypeName), ArchitectureDeclaredTypeFact>
            factsByAssemblyAndName = new();

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            factsByName.TryAdd(fact.FullTypeName, fact);
            factsByAssemblyAndName[(fact.AssemblyName, fact.FullTypeName)] = fact;
        }

        // Build secondary indexes (file and namespace lookups), sorting each list for determinism.
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byFile =
            new(StringComparer.Ordinal);
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byNamespace =
            new(StringComparer.Ordinal);

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            if (fact.SourceFilePath != null)
            {
                if (!byFile.TryGetValue(fact.SourceFilePath, out List<ArchitectureDeclaredTypeFact>? fileList))
                {
                    fileList = new List<ArchitectureDeclaredTypeFact>();
                    byFile[fact.SourceFilePath] = fileList;
                }

                fileList.Add(fact);
            }

            if (!byNamespace.TryGetValue(fact.Namespace, out List<ArchitectureDeclaredTypeFact>? nsList))
            {
                nsList = new List<ArchitectureDeclaredTypeFact>();
                byNamespace[fact.Namespace] = nsList;
            }

            nsList.Add(fact);
        }

        return new FactIndexData(
            factsByName,
            factsByAssemblyAndName,
            allFacts,
            ambiguities,
            byFile.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ArchitectureDeclaredTypeFact>)kvp.Value,
                StringComparer.Ordinal),
            byNamespace.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ArchitectureDeclaredTypeFact>)kvp.Value,
                StringComparer.Ordinal));
    }

    private static string NormalizePath(string repositoryRoot, string absoluteFilePath)
    {
        try
        {
            return Path.GetRelativePath(repositoryRoot, absoluteFilePath).Replace('\\', '/');
        }
        catch (Exception)
        {
            return absoluteFilePath.Replace('\\', '/');
        }
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

    private static IReadOnlyList<string> GetFolderSegments(string normalizedRelativePath)
    {
        int lastSlash = normalizedRelativePath.LastIndexOf('/');
        if (lastSlash <= 0) return Array.Empty<string>();
        string dir = normalizedRelativePath[..lastSlash];
        return dir.Split('/');
    }

    private static IReadOnlyList<string> GetNamespaceSegments(string ns) =>
        string.IsNullOrEmpty(ns)
            ? Array.Empty<string>()
            : ns.Split('.');

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
        Dictionary<string, ArchitectureDeclaredTypeFact> FactsByName,
        Dictionary<(string AssemblyName, string FullTypeName), ArchitectureDeclaredTypeFact> FactsByAssemblyAndName,
        IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts,
        IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByFile,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByNamespace);
}
