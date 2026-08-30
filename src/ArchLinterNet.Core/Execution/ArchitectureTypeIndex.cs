using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureTypeIndex
{
    private readonly IReadOnlyCollection<Assembly> _targetAssemblies;
    private readonly int _maxParallelism;
    private readonly AnalysisSessionProfilingCounters? _profilingCounters;
    private readonly IBoundedParallelPartitionRunner _partitionRunner;
    private readonly Func<Assembly, CancellationToken, ArchitectureLoadableTypeScan> _loadableTypeScanProvider;
    private readonly CancellationToken _cancellationToken;
    private readonly Lazy<ArchitectureLoadableTypeScan> _allTypeScan;

    public ArchitectureTypeIndex(IReadOnlyCollection<Assembly> targetAssemblies, CancellationToken cancellationToken = default)
        : this(targetAssemblies, maxParallelism: 0, profilingCounters: null, cancellationToken)
    {
    }

    internal ArchitectureTypeIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        int maxParallelism,
        AnalysisSessionProfilingCounters? profilingCounters,
        CancellationToken cancellationToken = default,
        IBoundedParallelPartitionRunner? partitionRunner = null,
        Func<Assembly, CancellationToken, IEnumerable<Type>>? loadableTypesProvider = null)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _maxParallelism = maxParallelism;
        _profilingCounters = profilingCounters;
        _partitionRunner = partitionRunner ?? new BoundedParallelPartitionRunner();
        _loadableTypeScanProvider = loadableTypesProvider is null
            ? ArchitectureTypeScanner.GetLoadableTypesWithCompleteness
            : (assembly, token) => new ArchitectureLoadableTypeScan(
                loadableTypesProvider(assembly, token).ToArray(),
                IsComplete: true);
        _cancellationToken = cancellationToken;
        _allTypeScan = new Lazy<ArchitectureLoadableTypeScan>(LoadAllTypes);
    }

    public Type[] AllTypes()
    {
        return _allTypeScan.Value.Types;
    }

    // Metrics use this to fail closed when Assembly.GetTypes recovered only a partial native type
    // universe. The existing AllTypes API deliberately retains its best-effort validation shape.
    internal bool HasCompleteTypeUniverse => _allTypeScan.Value.IsComplete;

    public Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return _allTypeScan.Value.Types
            .Where(type => ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)))
            .ToArray();
    }

    internal Type[] FindTypesInLayer(
        ArchitectureLayer layer, ArchitectureRoleIndex roleIndex, ArchitectureExpressionFactService expressionFacts)
    {
        return _allTypeScan.Value.Types
            .Where(type => ArchitectureLayerTypeMatcher.Matches(layer, type, roleIndex, expressionFacts))
            .ToArray();
    }

    public Type[] FindTypesInNamespace(string namespacePrefix)
    {
        return _allTypeScan.Value.Types
            .Where(type => ArchitectureLayerResolver.MatchesPrefix(
                ArchitectureTypeNames.SafeNamespace(type), namespacePrefix))
            .ToArray();
    }

    public HashSet<string> FindDirectChildNamespaces(string containerNamespace)
    {
        string prefix = containerNamespace + ".";
        HashSet<string> children = new(StringComparer.Ordinal);

        foreach (Type type in _allTypeScan.Value.Types)
        {
            string ns = ArchitectureTypeNames.SafeNamespace(type);
            if (!ns.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string remainder = ns[prefix.Length..];
            int dotIndex = remainder.IndexOf('.');
            string child = dotIndex < 0 ? remainder : remainder[..dotIndex];
            children.Add($"{prefix}{child}");
        }

        return children;
    }

    private ArchitectureLoadableTypeScan LoadAllTypes()
    {
        // Bounded-parallel across assemblies (issue #408): each assembly's type load is
        // independent and read-only, so partitions can run concurrently. The merge step below
        // flattens strictly in original assembly order — never completion order — so output is
        // byte-identical to the prior sequential implementation at every parallelism level. See
        // openspec/specs/bounded-parallel-scanning/spec.md, "Type loading is parallelized without
        // changing output order or content".
        List<Assembly> assemblies = _targetAssemblies.Distinct().ToList();
        ArchitectureLoadableTypeScan[] perAssemblyTypes = _partitionRunner.Run(
            assemblies,
            _maxParallelism,
            (assembly, _) =>
            {
                // Checked before the potentially long Assembly.GetTypes() call inside
                // GetLoadableTypes, not only inside its own per-type iterator — matches the
                // sequential path's per-assembly boundary check. See
                // ArchitectureTypeIndexBoundedParallelTests for a regression test that proves this
                // specific line — not the partition runner's own pre-iteration check, nor
                // GetLoadableTypes' own later per-type check — is what stops execution here.
                _cancellationToken.ThrowIfCancellationRequested();
                return _loadableTypeScanProvider(assembly, _cancellationToken);
            },
            _cancellationToken,
            _profilingCounters);

        List<Type> types = new();
        bool isComplete = true;
        foreach (ArchitectureLoadableTypeScan assemblyTypes in perAssemblyTypes)
        {
            types.AddRange(assemblyTypes.Types);
            isComplete &= assemblyTypes.IsComplete;
        }

        return new ArchitectureLoadableTypeScan(types.ToArray(), isComplete);
    }
}
