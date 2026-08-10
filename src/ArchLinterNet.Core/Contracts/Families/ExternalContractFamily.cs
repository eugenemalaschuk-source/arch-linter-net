using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_external")]
    public List<ArchitectureExternalDependencyContract> StrictExternal { get; set; } = new();

    [YamlMember(Alias = "audit_external")]
    public List<ArchitectureExternalDependencyContract> AuditExternal { get; set; } = new();
}

public sealed class ArchitectureExternalDependencyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    // External dependency contracts select their source by declared layer name, not assembly name.
    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Layer;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureExternalDependencyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Forbidden = new(Forbidden);
        return clone;
    }
}
