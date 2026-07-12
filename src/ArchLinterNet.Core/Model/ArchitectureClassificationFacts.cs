namespace ArchLinterNet.Core.Model;

public enum ArchitectureClassificationSource
{
    TypeAttribute,
    AssemblyAttribute,
    Inheritance,
    Namespace
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
// Evidence is the full type name of the attribute whose mapping produced Role/Metadata — the
// concrete fact backing the assignment, distinct from Source (which only names the mechanism).
public sealed record ArchitectureClassificationRoleFact(
    string Subject,
    string Role,
    ArchitectureClassificationSource Source,
    string? Evidence,
    IReadOnlyDictionary<string, object> Metadata);

// Recorded once per policy load when classification.path declares at least one entry - path-convention
// classification depends on source/declared-type fact discovery (issue #171) and produces no role
// assignment yet. Distinct from the fully silent overrides/exclusions reserved sections: declaring
// path is visible, not just schema-accepted.
public sealed record ArchitectureClassificationPathDeferredNotice(int DeclaredEntryCount);
