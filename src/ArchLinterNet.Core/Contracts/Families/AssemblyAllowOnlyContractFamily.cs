using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> StrictAssemblyAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> AuditAssemblyAllowOnly { get; set; } = new();
}

public sealed class ArchitectureAssemblyAllowOnlyContract : ArchitectureSourceExpandableContractBase
{
    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlIgnore] public override ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public override IArchitectureSourceExpandableContract CloneForSource(string source)
    {
        ArchitectureAssemblyAllowOnlyContract clone = new();
        CopyBaseFieldsTo(clone, source);
        clone.Allowed = new(Allowed);
        clone.DependencyDepth = DependencyDepth;
        return clone;
    }
}
