using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_independence")]
    public List<ArchitectureIndependenceContract> StrictIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_independence")]
    public List<ArchitectureIndependenceContract> AuditIndependence { get; set; } = new();
}

public sealed class ArchitectureIndependenceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
