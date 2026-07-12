using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblings { get; set; } = new();

    [YamlMember(Alias = "audit_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblings { get; set; } = new();
}

public sealed class ArchitectureAcyclicSiblingContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "ancestors")] public List<string> Ancestors { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
