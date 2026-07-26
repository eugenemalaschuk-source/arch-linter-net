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

public sealed class ArchitectureFrameworkReferenceAllowOnlyContract : IArchitectureSourceExpandableContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "sources")] public List<string> Sources { get; set; } = new();

    [YamlMember(Alias = "source_sets")] public List<string> SourceSets { get; set; } = new();

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlIgnore] public ArchitectureSourceExpansionOrigin? ExpansionOrigin { get; set; }

    [YamlIgnore] public ArchitectureSourceSetKind SourceKind => ArchitectureSourceSetKind.Assembly;

    public IArchitectureSourceExpandableContract CloneForSource(string source) =>
        new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = Name,
            Id = Id,
            Source = source,
            Allowed = new(Allowed),
            IgnoredViolations = new(IgnoredViolations),
            Reason = Reason
        };
}
