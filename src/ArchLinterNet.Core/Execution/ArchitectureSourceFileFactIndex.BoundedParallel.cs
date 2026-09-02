using System.Reflection;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Issue #408: per-partition worker bodies invoked by BoundedParallelPartitionRunner from
// RunReflectionPass/RunSourceScan in the main file. Split out to keep the main file under the
// project's file-size lint threshold — see openspec/specs/bounded-parallel-scanning/spec.md.
public sealed partial class ArchitectureSourceFileFactIndex
{
    // Do not force lazy source materialization merely to publish an input manifest. If a contract
    // consumed source text, BuildData retained the exact files successfully passed to the parser;
    // otherwise there are no source files to protect from this analysis session.
    internal IReadOnlyList<string> ConsumedSourceInputPaths => _data.IsValueCreated
        ? _data.Value.ConsumedSourceInputPaths
        : Array.Empty<string>();

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

    // Step 2: parse every *.cs file under each configured source root and map
    // (assemblyName, fullTypeName) → [(file, kind)]. Preprocessor symbols are forwarded so
    // conditional declarations match the compiled assembly. Each file is correlated only when its
    // owning assembly can be determined from the most specific known project subtree.
    private SourceScanResult RunSourceScan()
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

        Dictionary<SourceFactKey, List<SourceDeclaration>> sourceMap = [];
        List<string> consumedSourceInputPaths = [];
        foreach (SourceScanResult rootResult in perRootResults)
        {
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

    private sealed record SourceScanResult(
        Dictionary<SourceFactKey, List<SourceDeclaration>> SourceMap,
        IReadOnlyList<string> ConsumedSourceInputPaths)
    {
        public static SourceScanResult Empty { get; } = new([], Array.Empty<string>());
    }
}
