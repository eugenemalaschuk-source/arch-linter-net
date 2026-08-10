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

public sealed class ArchitectureAssemblyDependencyContract : IArchitectureSourceExpandableContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "sources")] public List<string> Sources { get; set; } = new();

    [YamlMember(Alias = "source_sets")] public List<string> SourceSets { get; set; } = new();

    [YamlMember(Alias = "exclude_sources")] public List<string> ExcludedSources { get; set; } = new();

    [YamlMember(Alias = "exclude_source_sets")] public List<string> ExcludedSourceSets { get; set; } = new();

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlIgnore] public ArchitectureSourceExpansionOrigin? ExpansionOrigin { get; set; }

    [YamlIgnore] public ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public IArchitectureSourceExpandableContract CloneForSource(string source) =>
        new ArchitectureAssemblyDependencyContract
        {
            Name = Name,
            Id = Id,
            Source = source,
            Forbidden = new(Forbidden),
            DependencyDepth = DependencyDepth,
            IgnoredViolations = new(IgnoredViolations),
            Reason = Reason
        };
}
