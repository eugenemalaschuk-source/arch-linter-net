namespace ArchLinterNet.Core.Contracts.Families;

// Per-family strict/audit [YamlMember] properties and contract POCOs live in
// Contracts/Families/*.cs, one file per contract family, contributed via this partial class.
// ArchitectureContractFamilyBindings is the single source of truth for which families exist.
public sealed partial class ArchitectureContractGroups
{
    public IEnumerable<IArchitectureContract> AllStrict =>
        ArchitectureContractFamilyBindings.All
            .Where(binding => binding.IncludeInContractEnumeration)
            .SelectMany(binding => binding.Strict(this));

    public IEnumerable<IArchitectureContract> AllAudit =>
        ArchitectureContractFamilyBindings.All
            .Where(binding => binding.IncludeInContractEnumeration)
            .SelectMany(binding => binding.Audit(this));
}
