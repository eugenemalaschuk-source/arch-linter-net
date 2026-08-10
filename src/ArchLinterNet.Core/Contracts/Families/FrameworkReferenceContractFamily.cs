using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> StrictFrameworkDependency { get; set; } = new();

    [YamlMember(Alias = "audit_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> AuditFrameworkDependency { get; set; } = new();
}

public sealed class ArchitectureFrameworkReferenceContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureFrameworkReferenceContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Forbidden = new(Forbidden);
        return clone;
    }
}
