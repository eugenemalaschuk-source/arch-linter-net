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
    private readonly BoundedParallelPartitionRunner _partitionRunner;
    private readonly CancellationToken _cancellationToken;
    private readonly Lazy<Type[]> _allTypes;

    public ArchitectureTypeIndex(IReadOnlyCollection<Assembly> targetAssemblies, CancellationToken cancellationToken = default)
        : this(targetAssemblies, maxParallelism: 0, profilingCounters: null, cancellationToken)
    {
    }

    internal ArchitectureTypeIndex(
        IReadOnlyCollection<Assembly> targetAssemblies,
        int maxParallelism,
        AnalysisSessionProfilingCounters? profilingCounters,
        CancellationToken cancellationToken = default,
        BoundedParallelPartitionRunner? partitionRunner = null)
    {
        _targetAssemblies = targetAssemblies ?? throw new ArgumentNullException(nameof(targetAssemblies));
        _maxParallelism = maxParallelism;
        _profilingCounters = profilingCounters;
        _partitionRunner = partitionRunner ?? new BoundedParallelPartitionRunner();
        _cancellationToken = cancellationToken;
        _allTypes = new Lazy<Type[]>(LoadAllTypes);
    }

    public Type[] AllTypes()
    {
        return _allTypes.Value;
    }

    public Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return _allTypes.Value
            .Where(type => ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)))
            .ToArray();
    }

    internal Type[] FindTypesInLayer(
        ArchitectureLayer layer, ArchitectureRoleIndex roleIndex, ArchitectureExpressionFactService expressionFacts)
    {
        return _allTypes.Value
            .Where(type => ArchitectureLayerTypeMatcher.Matches(layer, type, roleIndex, expressionFacts))
            .ToArray();
    }

    public Type[] FindTypesInNamespace(string namespacePrefix)
    {
        return _allTypes.Value
            .Where(type => ArchitectureLayerResolver.MatchesPrefix(
                ArchitectureTypeNames.SafeNamespace(type), namespacePrefix))
            .ToArray();
    }

    public HashSet<string> FindDirectChildNamespaces(string containerNamespace)
    {
        string prefix = containerNamespace + ".";
        HashSet<string> children = new(StringComparer.Ordinal);

        foreach (Type type in _allTypes.Value)
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

    private Type[] LoadAllTypes()
    {
        // Bounded-parallel across assemblies (issue #408): each assembly's type load is
        // independent and read-only, so partitions can run concurrently. The merge step below
        // flattens strictly in original assembly order — never completion order — so output is
        // byte-identical to the prior sequential implementation at every parallelism level. See
        // openspec/specs/bounded-parallel-scanning/spec.md, "Type loading is parallelized without
        // changing output order or content".
        List<Assembly> assemblies = _targetAssemblies.Distinct().ToList();
        Type[][] perAssemblyTypes = _partitionRunner.Run(
            assemblies,
            _maxParallelism,
            (assembly, _) => ArchitectureTypeScanner.GetLoadableTypes(assembly, _cancellationToken).ToArray(),
            _cancellationToken,
            _profilingCounters);

        List<Type> types = new();
        foreach (Type[] assemblyTypes in perAssemblyTypes)
        {
            types.AddRange(assemblyTypes);
        }

        return types.ToArray();
    }
}
