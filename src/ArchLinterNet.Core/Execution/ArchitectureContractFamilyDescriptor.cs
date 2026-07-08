using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

// Catalog metadata for one contract family, owned by ArchitectureContractFamilyRegistry and
// consumed by ArchitectureContractCatalog.Build. OwnedContractTypes and AdditionalValidation are
// inert extension surface reserved for future family-decomposition tasks (see issue #208); neither
// is read by any production code path in this change.
internal sealed record ArchitectureContractFamilyDescriptor(
    string FamilyId,
    string StrictGroupName,
    string AuditGroupName,
    bool IsBaselineCapable,
    Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> StrictContracts,
    Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> AuditContracts)
{
    public IReadOnlyList<Type> OwnedContractTypes { get; init; } = Array.Empty<Type>();

    public Action<ArchitectureContractDocument>? AdditionalValidation { get; init; }
}
