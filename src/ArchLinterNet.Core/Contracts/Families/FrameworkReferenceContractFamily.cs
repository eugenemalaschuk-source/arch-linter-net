using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> StrictFrameworkDependency { get; set; } = new();

    [YamlMember(Alias = "audit_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> AuditFrameworkDependency { get; set; } = new();
}

public sealed class ArchitectureFrameworkReferenceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
