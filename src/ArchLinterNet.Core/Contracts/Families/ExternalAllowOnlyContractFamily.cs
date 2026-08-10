using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_external_allow_only")]
    public List<ArchitectureExternalAllowOnlyContract> StrictExternalAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_external_allow_only")]
    public List<ArchitectureExternalAllowOnlyContract> AuditExternalAllowOnly { get; set; } = new();
}

public sealed class ArchitectureExternalAllowOnlyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    // External allow-only contracts select their source by declared layer name, not assembly name.
    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Layer;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureExternalAllowOnlyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Allowed = new(Allowed);
        clone.AllowedTypes = new(AllowedTypes);
        return clone;
    }
}
