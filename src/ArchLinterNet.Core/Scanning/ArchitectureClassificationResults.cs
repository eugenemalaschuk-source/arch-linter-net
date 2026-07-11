namespace ArchLinterNet.Core.Scanning;

public enum ArchitectureClassificationSource
{
    TypeAttribute,
    AssemblyAttribute
}

// Subject is the type full name for a type_attribute fact, or the assembly's simple name for an
// assembly_attribute fact — assembly-level facts are recorded once per assembly, not once per type.
public sealed record ArchitectureClassificationConflict(
    string Subject,
    ArchitectureClassificationSource Source,
    string WinningRole,
    string DiscardedRole);

public sealed record ArchitectureClassificationMetadataFailure(
    string Subject,
    ArchitectureClassificationSource Source,
    string MetadataKey,
    string Reason);

public sealed class ArchitectureTypeClassificationResult
{
    public static readonly ArchitectureTypeClassificationResult Empty = new(
        null, null, new Dictionary<string, object>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());

    public ArchitectureTypeClassificationResult(
        string? role,
        ArchitectureClassificationSource? source,
        IReadOnlyDictionary<string, object> metadata,
        IReadOnlyList<ArchitectureClassificationConflict> conflicts,
        IReadOnlyList<ArchitectureClassificationMetadataFailure> metadataFailures)
    {
        Role = role;
        Source = source;
        Metadata = metadata;
        Conflicts = conflicts;
        MetadataFailures = metadataFailures;
    }

    public string? Role { get; }

    public ArchitectureClassificationSource? Source { get; }

    public IReadOnlyDictionary<string, object> Metadata { get; }

    public IReadOnlyList<ArchitectureClassificationConflict> Conflicts { get; }

    public IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures { get; }
}

// Per-source (type_attribute or assembly_attribute) resolved candidate, before the two sources are
// combined by precedence. Kept internal: callers only ever see the combined ArchitectureTypeClassificationResult.
internal sealed record ArchitectureAttributeClassificationCandidate(
    string? Role,
    IReadOnlyDictionary<string, object> Metadata,
    IReadOnlyList<ArchitectureClassificationConflict> Conflicts,
    IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
{
    public static readonly ArchitectureAttributeClassificationCandidate Empty = new(
        null, new Dictionary<string, object>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>());
}
