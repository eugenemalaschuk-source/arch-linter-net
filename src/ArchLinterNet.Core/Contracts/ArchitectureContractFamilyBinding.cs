namespace ArchLinterNet.Core.Contracts;

// Contracts-local family metadata. Deliberately separate from
// ArchLinterNet.Core.Execution.ArchitectureContractFamilyDescriptor: Contracts must not depend on
// Execution (see openspec/specs/policy-document-validation-pipeline/spec.md), so the two registries
// duplicate the family list rather than sharing one across the module boundary. Delegate-based
// accessors (not reflection) so a renamed Strict*/Audit* property fails to compile.
internal sealed record ArchitectureContractFamilyBinding(
    string FamilyId,
    Func<Families.ArchitectureContractGroups, IEnumerable<IArchitectureContract>> Strict,
    Func<Families.ArchitectureContractGroups, IEnumerable<IArchitectureContract>> Audit,
    bool IncludeInContractEnumeration);
