using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

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
