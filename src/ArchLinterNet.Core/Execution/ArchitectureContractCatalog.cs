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

    // The order new families first appear in Build below, deduplicated. ArchitectureContractExecutor
    // iterates this list to dispatch each family through the handler registry, so a new family becomes
    // executable as soon as it is added here (plus a handler/registration) with no executor changes.
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

        AddGroup("strict", "strict", "dependency", groups.Strict);
        AddGroup("audit", "audit", "dependency", groups.Audit);
        AddGroup("strict_layers", "strict", "layer", groups.StrictLayers);
        AddGroup("audit_layers", "audit", "layer", groups.AuditLayers);
        AddGroup("strict_allow_only", "strict", "allow_only", groups.StrictAllowOnly);
        AddGroup("audit_allow_only", "audit", "allow_only", groups.AuditAllowOnly);
        AddGroup("strict_cycles", "strict", "cycle", groups.StrictCycles);
        AddGroup("audit_cycles", "audit", "cycle", groups.AuditCycles);
        AddGroup("strict_acyclic_siblings", "strict", "acyclic_sibling", groups.StrictAcyclicSiblings);
        AddGroup("audit_acyclic_siblings", "audit", "acyclic_sibling", groups.AuditAcyclicSiblings);
        AddGroup("strict_method_body", "strict", "method_body", groups.StrictMethodBody);
        AddGroup("audit_method_body", "audit", "method_body", groups.AuditMethodBody);
        AddGroup("strict_independence", "strict", "independence", groups.StrictIndependence);
        AddGroup("audit_independence", "audit", "independence", groups.AuditIndependence);
        AddGroup("strict_protected", "strict", "protected", groups.StrictProtected);
        AddGroup("audit_protected", "audit", "protected", groups.AuditProtected);
        AddGroup("strict_external", "strict", "external", groups.StrictExternal);
        AddGroup("audit_external", "audit", "external", groups.AuditExternal);
        AddGroup("strict_asmdef", "strict", "asmdef", groups.StrictAsmdef);
        AddGroup("audit_asmdef", "audit", "asmdef", groups.AuditAsmdef);
        AddGroup("strict_coverage", "strict", "coverage", groups.StrictCoverage);
        AddGroup("audit_coverage", "audit", "coverage", groups.AuditCoverage);

        AddGroup("strict_layer_templates", "strict", "layer_template",
            LayerTemplateExpander.Expand(groups.StrictLayerTemplates));
        AddGroup("audit_layer_templates", "audit", "layer_template",
            LayerTemplateExpander.Expand(groups.AuditLayerTemplates));

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

    public string? ResolveGroup(IArchitectureContract contract)
    {
        foreach (ArchitectureContractDescriptor descriptor in _descriptors)
        {
            if (IsGroupResolvable(descriptor.Family) && ReferenceEquals(descriptor.Contract, contract))
            {
                return descriptor.Group;
            }
        }

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

    private static bool IsGroupResolvable(string family) => family is not ("asmdef" or "layer_template");
}
