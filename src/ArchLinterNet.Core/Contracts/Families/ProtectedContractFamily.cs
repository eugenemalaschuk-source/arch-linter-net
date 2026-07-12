using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_protected")]
    public List<ArchitectureProtectedContract> StrictProtected { get; set; } = new();

    [YamlMember(Alias = "audit_protected")]
    public List<ArchitectureProtectedContract> AuditProtected { get; set; } = new();
}

public sealed class ArchitectureProtectedContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "protected")] public List<string> Protected { get; set; } = new();

    [YamlMember(Alias = "allowed_importers")]
    public List<string> AllowedImporters { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
