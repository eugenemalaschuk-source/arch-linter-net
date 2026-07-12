using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Scanning;

public sealed class ArchitectureTypeClassificationResult
{
    public ArchitectureTypeClassificationResult(
        string? role,
        ArchitectureClassificationSource? source,
        IReadOnlyDictionary<string, object> metadata,
        string? evidence,
        IReadOnlyList<ArchitectureClassificationConflict> conflicts,
        IReadOnlyList<ArchitectureClassificationMetadataFailure> metadataFailures)
    {
        Role = role;
        Source = source;
        Metadata = metadata;
        Evidence = evidence;
        Conflicts = conflicts;
        MetadataFailures = metadataFailures;
    }

    public string? Role { get; }

    public ArchitectureClassificationSource? Source { get; }

    public IReadOnlyDictionary<string, object> Metadata { get; }

    // The full type name of the attribute whose mapping produced Role/Metadata — the concrete
    // evidence backing the role assignment, distinct from Source (which only names the mechanism:
    // type_attribute vs assembly_attribute). Null when Role is null.
    public string? Evidence { get; }

    public IReadOnlyList<ArchitectureClassificationConflict> Conflicts { get; }

    public IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures { get; }
}

// Per-source (type_attribute, assembly_attribute, inheritance, or namespace) resolved candidate,
// before sources are combined by precedence. Kept internal: callers only ever see the combined
// ArchitectureTypeClassificationResult.
internal sealed record ArchitectureAttributeClassificationCandidate(
    string? Role,
    IReadOnlyDictionary<string, object> Metadata,
    string? Evidence,
    IReadOnlyList<ArchitectureClassificationConflict> Conflicts,
    IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
{
    public static readonly ArchitectureAttributeClassificationCandidate Empty = new(
        null, new Dictionary<string, object>(), null,
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());
}
