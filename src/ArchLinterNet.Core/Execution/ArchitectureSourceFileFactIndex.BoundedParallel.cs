using System.Reflection;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Issue #408: per-partition worker bodies invoked by BoundedParallelPartitionRunner from
// RunReflectionPass/RunSourceScan in the main file. Split out to keep the main file under the
// project's file-size lint threshold — see openspec/specs/bounded-parallel-scanning/spec.md.
public sealed partial class ArchitectureSourceFileFactIndex
{
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
                GetTypeKindFromReflection(type)));
        }

        return factsByName;
    }

    private Dictionary<SourceFactKey, List<SourceDeclaration>> ScanSourceRoot(
        string sourceRoot,
        IReadOnlyList<(string SourceRoot, string AssemblyName)> ownershipEntries)
    {
        Dictionary<SourceFactKey, List<SourceDeclaration>> localMap = [];
        _cancellationToken.ThrowIfCancellationRequested();
        string normalizedSourceRoot = NormalizeRelativePath(sourceRoot);
        string absoluteRoot = Path.Combine(_repositoryRoot, normalizedSourceRoot);
        if (!_fileSystem.DirectoryExists(absoluteRoot))
        {
            return localMap;
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

            ProcessSourceFile(localMap, assemblyName, absoluteRoot, absoluteFile);
        }

        return localMap;
    }
}
