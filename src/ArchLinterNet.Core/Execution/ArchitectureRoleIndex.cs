using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Per-run, lazily-computed index of each type's resolved semantic role (role, metadata,
// classification source, evidence), built from a single ArchitectureAttributeRoleExtractor pass
// over the session's type universe. Mirrors ArchitectureTypeIndex's Lazy<T>-on-first-access shape:
// scoped to one ArchitectureAnalysisSession, never reused across assemblies or runs.
// Conflicts/MetadataFailures are computed alongside role descriptors in the same pass, replacing
// ArchitectureAnalysisSession.CheckClassificationFacts()'s former re-run-per-call behavior.
public sealed class ArchitectureRoleIndex
{
    private readonly ArchitectureClassificationConfiguration _configuration;
    private readonly ArchitectureTypeIndex _typeIndex;
    private readonly Lazy<RoleIndexData> _data;

    public ArchitectureRoleIndex(ArchitectureClassificationConfiguration configuration, ArchitectureTypeIndex typeIndex)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _typeIndex = typeIndex ?? throw new ArgumentNullException(nameof(typeIndex));
        _data = new Lazy<RoleIndexData>(BuildData);
    }

    public IReadOnlyList<ArchitectureClassificationConflict> Conflicts => _data.Value.Conflicts;

    public IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures => _data.Value.MetadataFailures;

    public bool TryGetRole(Type type, out ArchitectureTypeClassificationResult descriptor)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_data.Value.RolesByType.TryGetValue(type, out descriptor!))
        {
            return true;
        }

        // The index is built from assembly.GetTypes(), which only ever yields open generic type
        // definitions (e.g. IPaymentPort<>) - reflection on a concrete adapter/reference reports the
        // closed constructed type (e.g. IPaymentPort<Order>) instead, which never equals the open
        // definition as a dictionary key. Falling back to the generic type definition lets a
        // classification attribute declared on the open type apply to every closed instantiation.
        if (type.IsConstructedGenericType)
        {
            return _data.Value.RolesByType.TryGetValue(type.GetGenericTypeDefinition(), out descriptor!);
        }

        return false;
    }

    public IReadOnlyCollection<Type> ClassifiedTypes()
    {
        return _data.Value.RolesByType.Keys;
    }

    private RoleIndexData BuildData()
    {
        if (_configuration.Attributes.Count == 0 && _configuration.AssemblyAttributes.Count == 0
            && _configuration.Inheritance.Count == 0 && _configuration.Namespace.Count == 0)
        {
            return RoleIndexData.Empty;
        }

        Type[] types = _typeIndex.AllTypes();
        var extractor = new ArchitectureAttributeRoleExtractor(_configuration, types, MatchNamespaceMapping);

        Dictionary<Type, ArchitectureTypeClassificationResult> rolesByType = new();
        HashSet<ArchitectureClassificationConflict> conflicts = new();
        HashSet<ArchitectureClassificationMetadataFailure> metadataFailures = new();

        foreach (Type type in types)
        {
            ArchitectureTypeClassificationResult result = extractor.Extract(type);
            conflicts.UnionWith(result.Conflicts);
            metadataFailures.UnionWith(result.MetadataFailures);

            if (result.Role != null)
            {
                rolesByType[type] = result;
            }
        }

        return new RoleIndexData(rolesByType, conflicts.ToList(), metadataFailures.ToList());
    }

    // Reuses ArchitectureLayerResolver's namespace-glob matching (the same mechanism layers.<name>.namespace
    // already uses) instead of duplicating glob logic in Scanning, which must not depend on Resolution
    // (see docs/internal/core-architecture-blueprint.md). Returns a plain tuple, not the Resolution-typed
    // ArchitectureNamespaceMatch, so Scanning never needs a reference to Resolution types, even in a
    // delegate signature. A mapping declaring only namespace_suffix (valid per schema, unlike layers,
    // which require namespace whenever namespace_suffix is present) is matched directly here since
    // ArchitectureLayerResolver.MatchNamespace short-circuits on an empty Namespace.
    private static (bool Matched, string? MatchedPattern) MatchNamespaceMapping(
        ArchitectureNamespaceClassificationMapping mapping, string candidateNamespace)
    {
        if (mapping.Namespace.Length > 0)
        {
            var layer = new ArchitectureLayer { Namespace = mapping.Namespace, NamespaceSuffix = mapping.NamespaceSuffix };
            ArchitectureNamespaceMatch match = ArchitectureLayerResolver.MatchNamespace(layer, candidateNamespace);
            return (match.Matched, match.Matched ? mapping.Namespace : null);
        }

        if (mapping.NamespaceSuffix.Length > 0)
        {
            bool matched = candidateNamespace == mapping.NamespaceSuffix
                || candidateNamespace.EndsWith("." + mapping.NamespaceSuffix, StringComparison.Ordinal);
            return (matched, matched ? $"*.{mapping.NamespaceSuffix}" : null);
        }

        return (false, null);
    }

    private sealed record RoleIndexData(
        Dictionary<Type, ArchitectureTypeClassificationResult> RolesByType,
        IReadOnlyList<ArchitectureClassificationConflict> Conflicts,
        IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
    {
        public static readonly RoleIndexData Empty = new(
            new Dictionary<Type, ArchitectureTypeClassificationResult>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>());
    }
}
