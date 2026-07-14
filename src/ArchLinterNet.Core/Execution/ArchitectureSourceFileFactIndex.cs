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
        List<Assembly> sortedAssemblies = _targetAssemblies
            .Distinct()
            .OrderBy(a => a.GetName().Name ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        Dictionary<string, List<BaseFact>> reflectionFacts = RunReflectionPass(sortedAssemblies);

        HashSet<string> multiAssemblyNames = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<BaseFact>> kvp in reflectionFacts)
            if (kvp.Value.Count > 1) multiAssemblyNames.Add(kvp.Key);

        Dictionary<string, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap =
            _sourceRoots.Count > 0 ? RunSourceScan() : [];

        (Dictionary<string, SourceInfo> resolvedSourceInfo,
            List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities) =
                ResolveSourceInfo(sourceMap, multiAssemblyNames);

        List<ArchitectureDeclaredTypeFact> allFacts = BuildFacts(reflectionFacts, resolvedSourceInfo);

        allFacts.Sort((a, b) =>
        {
            int c = StringComparer.Ordinal.Compare(a.FullTypeName, b.FullTypeName);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.AssemblyName, b.AssemblyName);
        });
        ambiguities.Sort((a, b) => StringComparer.Ordinal.Compare(a.FullTypeName, b.FullTypeName));

        Dictionary<string, ArchitectureDeclaredTypeFact> factsByName = new(StringComparer.Ordinal);
        Dictionary<(string AssemblyName, string FullTypeName), ArchitectureDeclaredTypeFact>
            factsByAssemblyAndName = new();

        foreach (ArchitectureDeclaredTypeFact fact in allFacts)
        {
            factsByName.TryAdd(fact.FullTypeName, fact);
            factsByAssemblyAndName[(fact.AssemblyName, fact.FullTypeName)] = fact;
        }

        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byFile = new(StringComparer.Ordinal);
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byNamespace = new(StringComparer.Ordinal);

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

    // Step 1: walk every loadable type in each assembly and collect one BaseFact per
    // (assemblyName, fullTypeName). Assemblies are already sorted alphabetically before this call.
    private Dictionary<string, List<BaseFact>> RunReflectionPass(List<Assembly> sortedAssemblies)
    {
        Dictionary<string, List<BaseFact>> factsByName = new(StringComparer.Ordinal);
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
                list.Add(new BaseFact(assemblyName, ns, fullName,
                    GetSimpleTypeName(type), GetTypeKindFromReflection(type)));
            }
        }
        return factsByName;
    }

    // Step 2: parse every *.cs file under each source root and map FullTypeName → [(file, kind)].
    // Preprocessor symbols are forwarded so conditional declarations match the compiled assembly.
    private Dictionary<string, List<(string FilePath, ArchitectureTypeKind Kind)>> RunSourceScan()
    {
        Dictionary<string, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap =
            new(StringComparer.Ordinal);

        foreach (string sourceRoot in _sourceRoots)
        {
            string absoluteRoot = Path.Combine(_repositoryRoot, sourceRoot);
            if (!_fileSystem.DirectoryExists(absoluteRoot)) continue;

            foreach (string absoluteFile in _fileSystem.EnumerateFiles(
                absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                // Relative to the scanned root so ancestor directory names outside the repo
                // can never be mistaken for excluded segments.
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
                    if (!sourceMap.TryGetValue(parsed.FullTypeName,
                        out List<(string, ArchitectureTypeKind)>? entries))
                    {
                        entries = [];
                        sourceMap[parsed.FullTypeName] = entries;
                    }
                    entries.Add((normalizedFilePath, parsed.TypeKind));
                }
            }
        }

        return sourceMap;
    }

    // Step 3: for each CLR name found only in a single assembly, resolve it to either one
    // source file (enriched) or an ambiguity (partial class across multiple files). CLR names
    // shared by multiple assemblies are skipped — ownership is indeterminate without per-project
    // source roots, so those facts keep null SourceFilePath rather than getting a wrong path.
    private static (
        Dictionary<string, SourceInfo> Resolved,
        List<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities)
        ResolveSourceInfo(
            Dictionary<string, List<(string FilePath, ArchitectureTypeKind Kind)>> sourceMap,
            HashSet<string> multiAssemblyNames)
    {
        Dictionary<string, SourceInfo> resolved = new(StringComparer.Ordinal);
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = [];

        foreach (KeyValuePair<string, List<(string FilePath, ArchitectureTypeKind Kind)>> entry in sourceMap)
        {
            string fullName = entry.Key;
            if (multiAssemblyNames.Contains(fullName)) continue;

            // Deduplicate by path: overlapping roots or multiple declarations in one file are NOT ambiguous.
            List<string> uniquePaths = entry.Value
                .Select(e => e.FilePath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            if (uniquePaths.Count == 1)
            {
                string relPath = uniquePaths[0];
                ArchitectureTypeKind kind = entry.Value.First(e => e.FilePath == relPath).Kind;
                resolved[fullName] = new SourceInfo(relPath, kind, IsAmbiguous: false);
            }
            else if (uniquePaths.Count > 1)
            {
                ambiguities.Add(new ArchitectureDeclaredTypeSourceAmbiguity(fullName, uniquePaths));
                resolved[fullName] = new SourceInfo(null, ArchitectureTypeKind.Unknown, IsAmbiguous: true);
            }
        }

        return (resolved, ambiguities);
    }

    // Step 4: emit one ArchitectureDeclaredTypeFact per (assemblyName, fullTypeName) pair,
    // applying source enrichment where available.
    private static List<ArchitectureDeclaredTypeFact> BuildFacts(
        Dictionary<string, List<BaseFact>> reflectionFactsByName,
        Dictionary<string, SourceInfo> resolvedSourceInfo)
    {
        List<ArchitectureDeclaredTypeFact> allFacts = [];

        foreach (KeyValuePair<string, List<BaseFact>> entry in reflectionFactsByName
            .OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string fullName = entry.Key;
            resolvedSourceInfo.TryGetValue(fullName, out SourceInfo? si);

            foreach (BaseFact bf in entry.Value)
            {
                allFacts.Add(si is { IsAmbiguous: true }
                    ? new ArchitectureDeclaredTypeFact(
                        bf.AssemblyName, bf.Namespace, fullName, bf.SimpleTypeName,
                        bf.TypeKind, null, null, [], GetNamespaceSegments(bf.Namespace))
                    : si?.FilePath != null
                        ? new ArchitectureDeclaredTypeFact(
                            bf.AssemblyName, bf.Namespace, fullName, bf.SimpleTypeName,
                            si.KindFromSource, si.FilePath,
                            GetFileNameWithoutExtension(si.FilePath),
                            GetFolderSegments(si.FilePath),
                            GetNamespaceSegments(bf.Namespace))
                        : new ArchitectureDeclaredTypeFact(
                            bf.AssemblyName, bf.Namespace, fullName, bf.SimpleTypeName,
                            bf.TypeKind, null, null, [], GetNamespaceSegments(bf.Namespace)));
            }
        }

        return allFacts;
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
