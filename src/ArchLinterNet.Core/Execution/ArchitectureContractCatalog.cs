using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public sealed record ArchitectureContractDescriptor(
    string Group,
    string Mode,
    string Family,
    string Name,
    string? Id,
    IArchitectureContract Contract);

public sealed class ArchitectureContractCatalog
{
    private readonly List<ArchitectureContractDescriptor> _descriptors;
    private readonly List<string> _familiesInOrder;

    private ArchitectureContractCatalog(List<ArchitectureContractDescriptor> descriptors, List<string> familiesInOrder)
    {
        _descriptors = descriptors;
        _familiesInOrder = familiesInOrder;
    }

    // The order families first appear while Build below iterates ArchitectureContractFamilyRegistry.All,
    // deduplicated. ArchitectureContractExecutor iterates this list to dispatch each family through the
    // handler registry, so a new family becomes executable as soon as it has a descriptor in the registry
    // (plus a handler/registration) - no edits to this file are needed.
    public IReadOnlyList<string> FamiliesInOrder => _familiesInOrder;

    public static ArchitectureContractCatalog Build(ArchitectureContractDocument document)
    {
        ArchitectureContractGroups groups = document.Contracts;
        List<ArchitectureContractDescriptor> descriptors = new();
        List<string> familiesInOrder = new();
        HashSet<string> seenFamilies = new(StringComparer.Ordinal);

        void AddGroup<T>(string group, string mode, string family, IEnumerable<T> contracts)
            where T : IArchitectureContract
        {
            if (seenFamilies.Add(family))
            {
                familiesInOrder.Add(family);
            }

            foreach (T contract in contracts)
            {
                descriptors.Add(new ArchitectureContractDescriptor(group, mode, family, contract.Name, contract.Id, contract));
            }
        }

        // Family order here is observable: it determines ArchitectureContractExecutor's dispatch
        // order via FamiliesInOrder below, which in turn determines violation/cycle insertion order
        // in ValidationOutcome (and therefore JSON output) and --timings entry order. The order is
        // defined by ArchitectureContractFamilyRegistry.All, which mirrors the pre-registry executor's
        // family order exactly, so driving dispatch from the registry does not reorder observable
        // output.
        foreach (ArchitectureContractFamilyDescriptor family in ArchitectureContractFamilyRegistry.All)
        {
            AddGroup(family.StrictGroupName, "strict", family.FamilyId, family.StrictContracts(groups));
            AddGroup(family.AuditGroupName, "audit", family.FamilyId, family.AuditContracts(groups));
        }

        return new ArchitectureContractCatalog(descriptors, familiesInOrder);
    }

    public IEnumerable<IArchitectureContract> ContractsFor(string mode, string family)
    {
        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (descriptor.Mode == mode && descriptor.Family == family)
            {
                yield return descriptor.Contract;
            }
        }
    }

    public IEnumerable<IArchitectureContract> ContractsFor(string mode)
    {
        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (descriptor.Mode == mode)
            {
                yield return descriptor.Contract;
            }
        }
    }

    public HashSet<string> AvailableContractIds(string mode)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);

        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (descriptor.Mode == mode && descriptor.Id != null)
            {
                ids.Add(descriptor.Id);
            }
        }

        return ids;
    }

    // Distinct groups whose contracts can produce baseline candidates (every family except asmdef and
    // layer_templates, which never route through ArchitectureContractExecutionContext.IsIgnored). The
    // baseline model must be able to represent all of these; a guard test asserts the two sets match.
    public IReadOnlyCollection<string> BaselineCapableGroups()
    {
        HashSet<string> groups = new(StringComparer.Ordinal);
        groups.UnionWith(_descriptors
            .Where(d => IsGroupResolvable(d.Family))
            .Select(d => d.Group));
        return groups;
    }

    // Contract IDs declared in the policy for a given baseline group. The baseline comparer uses this
    // to tell a still-configured contract (frozen/resolved) from one whose id no longer exists in the
    // policy (configuration-error), sourcing "known ids" from the same catalog the executor runs.
    public HashSet<string> ContractIdsInGroup(string group)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);

        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (descriptor.Group == group && descriptor.Id != null)
            {
                ids.Add(descriptor.Id);
            }
        }

        return ids;
    }

    public string? ResolveGroup(IArchitectureContract contract)
    {
        string? group = _descriptors
            .FirstOrDefault(d => IsGroupResolvable(d.Family) && ReferenceEquals(d.Contract, contract))
            ?.Group;
        if (group != null) return group;

        if (contract.Id == null)
        {
            return null;
        }

        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (IsGroupResolvable(descriptor.Family)
                && descriptor.Id != null
                && string.Equals(descriptor.Id, contract.Id, StringComparison.OrdinalIgnoreCase))
            {
                return descriptor.Group;
            }
        }

        return null;
    }

    private static bool IsGroupResolvable(string family)
    {
        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            if (descriptor.FamilyId == family)
            {
                return descriptor.IsBaselineCapable;
            }
        }

        return false;
    }
}
