using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// Every source-expandable contract family (assembly/package/framework/external x
// dependency/allow-only) repeats the same Source/Sources/SourceSets/... plumbing the interface
// itself defines. This base holds exactly that shared shape so it is declared once; each family
// still declares its own list (Allowed/Forbidden/...), DependencyDepth where it applies, and
// SourceKind, and still writes its own CloneForSource body explicitly — CopyBaseFieldsTo only
// copies the fields defined here, so every family-specific field stays visible and reviewable at
// its own call site (see IArchitectureSourceExpandableContract.CloneForSource).
public abstract class ArchitectureSourceExpandableContractBase : IArchitectureSourceExpandableContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "sources")] public List<string> Sources { get; set; } = new();

    [YamlMember(Alias = "source_sets")] public List<string> SourceSets { get; set; } = new();

    [YamlMember(Alias = "exclude_sources")] public List<string> ExcludedSources { get; set; } = new();

    [YamlMember(Alias = "exclude_source_sets")] public List<string> ExcludedSourceSets { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlIgnore] public ArchitectureSourceExpansionOrigin? ExpansionOrigin { get; set; }

    [YamlIgnore] public abstract ArchitectureSourceSetKind SourceKind { get; }

    public abstract IArchitectureSourceExpandableContract CloneForSource(string source);

    protected void CopyBaseFieldsTo(ArchitectureSourceExpandableContractBase clone, string source)
    {
        clone.Name = Name;
        clone.Id = Id;
        clone.Source = source;
        clone.IgnoredViolations = new(IgnoredViolations);
        clone.Reason = Reason;
    }
}
