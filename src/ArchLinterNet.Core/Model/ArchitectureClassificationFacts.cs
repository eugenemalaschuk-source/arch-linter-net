namespace ArchLinterNet.Core.Model;

public enum ArchitectureClassificationSource
{
    TypeAttribute,
    AssemblyAttribute
}

// Subject is the type full name for a type_attribute fact, or the assembly's simple name for an
// assembly_attribute fact — assembly-level facts are recorded once per assembly, not once per type.
// MetadataDetail is a canonical, sorted "key: winningValue vs discardedValue" summary of every metadata
// key that actually differed between the two candidates, or null when the conflict was role-only —
// a plain string (not a dictionary) so this record keeps correct value equality for HashSet-based
// deduplication of recurring assembly-level facts across every type in that assembly.
public sealed record ArchitectureClassificationConflict(
    string Subject,
    ArchitectureClassificationSource Source,
    string WinningRole,
    string DiscardedRole,
    string? MetadataDetail);

public sealed record ArchitectureClassificationMetadataFailure(
    string Subject,
    ArchitectureClassificationSource Source,
    string MetadataKey,
    string Reason);

// A discovered role descriptor for one type, surfaced by the validate command alongside conflicts
// and metadata failures. Subject is the type's full name. Metadata mirrors the winning source's
// canonicalized values (string/boolean/decimal), per attribute-role-extraction canonicalization.
public sealed record ArchitectureClassificationRoleFact(
    string Subject,
    string Role,
    ArchitectureClassificationSource Source,
    IReadOnlyDictionary<string, object> Metadata);
