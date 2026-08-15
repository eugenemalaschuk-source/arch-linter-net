using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

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
