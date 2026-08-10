using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_package_dependency")]
    public List<ArchitecturePackageDependencyContract> StrictPackageDependency { get; set; } = new();

    [YamlMember(Alias = "audit_package_dependency")]
    public List<ArchitecturePackageDependencyContract> AuditPackageDependency { get; set; } = new();
}

public sealed class ArchitecturePackageDependencyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitecturePackageDependencyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Forbidden = new(Forbidden);
        clone.DependencyDepth = DependencyDepth;
        return clone;
    }
}
