namespace ArchLinterNet.Core.Model;

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
