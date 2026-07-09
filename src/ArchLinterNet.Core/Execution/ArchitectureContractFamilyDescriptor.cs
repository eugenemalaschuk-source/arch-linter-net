using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;

namespace ArchLinterNet.Core.Execution;

// Catalog metadata for one contract family, owned by ArchitectureContractFamilyRegistry and
// consumed by ArchitectureContractCatalog.Build. OwnedContractTypes and AdditionalValidation remain
// inert extension surface reserved for further family-decomposition work; neither is read by any
// production code path in this change. Checker, unlike those two, is live: it is the family's
// checker/handler behavior, read and invoked by ArchitectureContractHandlerRegistry (see #211).
internal sealed record ArchitectureContractFamilyDescriptor(
    string FamilyId,
    string StrictGroupName,
    string AuditGroupName,
    bool IsBaselineCapable,
    Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> StrictContracts,
    Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> AuditContracts,
    ArchitectureContractChecker Checker)
{
    public IReadOnlyList<Type> OwnedContractTypes { get; init; } = Array.Empty<Type>();

    public Action<ArchitectureContractDocument>? AdditionalValidation { get; init; }
}
