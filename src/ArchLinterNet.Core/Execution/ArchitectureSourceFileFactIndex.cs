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
// - CLR-format full names (dots, +, `N) used as index keys throughout.
// - Empty sourceRoots → reflection-only facts (null SourceFilePath) with no filesystem access.
// - Partial classes across files → ArchitectureDeclaredTypeSourceAmbiguity + null SourceFilePath.
// - Record detection requires Roslyn source analysis; reflection falls back to Class/Struct.
// - Paths normalized to forward slashes, relative to repositoryRoot.
public sealed class ArchitectureSourceFileFactIndex
{
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly Lazy<FactIndexData> _data;

    public ArchitectureSourceFileFactIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string repositoryRoot,
        IReadOnlyList<string> sourceRoots,
        IArchitectureFileSystem? fileSystem = null)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        _sourceRoots = sourceRoots ?? throw new ArgumentNullException(nameof(sourceRoots));
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
        // Step 1 — reflection pass: build a base fact for every loadable type.
        Dictionary<string, BaseFact> reflectionFacts =
            new(StringComparer.Ordinal);

        foreach (Assembly assembly in _targetAssemblies.Distinct())
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                string? fullName = SafeFullName(type);
                if (string.IsNullOrEmpty(fullName)) continue;

                string ns = SafeNamespace(type);
                reflectionFacts[fullName] = new BaseFact(
                    assemblyName, ns, fullName,
                    GetSimpleTypeName(type),
                    GetTypeKindFromReflection(type));
            }
        }

        // Step 2 — source scan: parse each .cs file and collect FullName → file+kind entries.
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
                        ArchitectureDeclaredTypeParser.ParseSourceText(sourceText))
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
        Dictionary<string, ArchitectureDeclaredTypeFact> factsByName =
            new(StringComparer.Ordinal);
        List<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = new();

        foreach (KeyValuePair<string, BaseFact> entry in reflectionFacts)
        {
            string fullName = entry.Key;
            BaseFact baseFact = entry.Value;

            if (sourceMap.TryGetValue(fullName, out List<(string FilePath, ArchitectureTypeKind Kind)>? sourceEntries))
            {
                if (sourceEntries.Count == 1)
                {
                    string relPath = sourceEntries[0].FilePath;
                    ArchitectureTypeKind kindFromSource = sourceEntries[0].Kind;

                    factsByName[fullName] = new ArchitectureDeclaredTypeFact(
                        baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                        kindFromSource,
                        relPath,
                        GetFileNameWithoutExtension(relPath),
                        GetFolderSegments(relPath),
                        GetNamespaceSegments(baseFact.Namespace));
                }
                else
                {
                    // Partial class across multiple files — ambiguous; null source path.
                    ambiguities.Add(new ArchitectureDeclaredTypeSourceAmbiguity(
                        fullName,
                        sourceEntries.Select(e => e.FilePath).ToList()));

                    factsByName[fullName] = new ArchitectureDeclaredTypeFact(
                        baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                        baseFact.TypeKind,
                        null, null, Array.Empty<string>(),
                        GetNamespaceSegments(baseFact.Namespace));
                }
            }
            else
            {
                // No matching source file — reflection-only fact.
                factsByName[fullName] = new ArchitectureDeclaredTypeFact(
                    baseFact.AssemblyName, baseFact.Namespace, fullName, baseFact.SimpleTypeName,
                    baseFact.TypeKind,
                    null, null, Array.Empty<string>(),
                    GetNamespaceSegments(baseFact.Namespace));
            }
        }

        // Build secondary indexes (file and namespace lookups).
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byFile =
            new(StringComparer.Ordinal);
        Dictionary<string, List<ArchitectureDeclaredTypeFact>> byNamespace =
            new(StringComparer.Ordinal);

        foreach (ArchitectureDeclaredTypeFact fact in factsByName.Values)
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
            factsByName.Values.ToList(),
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

    private sealed record FactIndexData(
        Dictionary<string, ArchitectureDeclaredTypeFact> FactsByName,
        IReadOnlyList<ArchitectureDeclaredTypeFact> AllFacts,
        IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> Ambiguities,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByFile,
        Dictionary<string, IReadOnlyList<ArchitectureDeclaredTypeFact>> ByNamespace);
}
