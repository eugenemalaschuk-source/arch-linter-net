using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
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
        return _data.Value.RolesByType.TryGetValue(type, out descriptor!);
    }

    public IReadOnlyCollection<Type> ClassifiedTypes()
    {
        return _data.Value.RolesByType.Keys;
    }

    private RoleIndexData BuildData()
    {
        if (_configuration.Attributes.Count == 0 && _configuration.AssemblyAttributes.Count == 0)
        {
            return RoleIndexData.Empty;
        }

        Type[] types = _typeIndex.AllTypes();
        var extractor = new ArchitectureAttributeRoleExtractor(_configuration, types);

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
