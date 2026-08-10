using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_assembly_dependency")]
    public List<ArchitectureAssemblyDependencyContract> StrictAssemblyDependency { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_dependency")]
    public List<ArchitectureAssemblyDependencyContract> AuditAssemblyDependency { get; set; } = new();
}

public sealed class ArchitectureAssemblyDependencyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureAssemblyDependencyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Forbidden = new(Forbidden);
        clone.DependencyDepth = DependencyDepth;
        return clone;
    }
}
