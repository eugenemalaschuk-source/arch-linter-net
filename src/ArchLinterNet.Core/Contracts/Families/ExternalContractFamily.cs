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

public sealed class ArchitectureExternalDependencyContract : IArchitectureSourceExpandableContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "sources")] public List<string> Sources { get; set; } = new();

    [YamlMember(Alias = "source_sets")] public List<string> SourceSets { get; set; } = new();

    [YamlMember(Alias = "exclude_sources")] public List<string> ExcludedSources { get; set; } = new();

    [YamlMember(Alias = "exclude_source_sets")] public List<string> ExcludedSourceSets { get; set; } = new();

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlIgnore] public ArchitectureSourceExpansionOrigin? ExpansionOrigin { get; set; }

    // External dependency contracts select their source by declared layer name, not assembly name.
    [YamlIgnore] public ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Layer;

    public IArchitectureSourceExpandableContract CloneForSource(string source) =>
        new ArchitectureExternalDependencyContract
        {
            Name = Name,
            Id = Id,
            Source = source,
            Forbidden = new(Forbidden),
            IgnoredViolations = new(IgnoredViolations),
            Reason = Reason
        };
}
