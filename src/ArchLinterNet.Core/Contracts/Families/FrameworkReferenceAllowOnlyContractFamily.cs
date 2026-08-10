using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_framework_allow_only")]
    public List<ArchitectureFrameworkReferenceAllowOnlyContract> StrictFrameworkAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_framework_allow_only")]
    public List<ArchitectureFrameworkReferenceAllowOnlyContract> AuditFrameworkAllowOnly { get; set; } = new();
}

public sealed class ArchitectureFrameworkReferenceAllowOnlyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureFrameworkReferenceAllowOnlyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Allowed = new(Allowed);
        return clone;
    }
}
