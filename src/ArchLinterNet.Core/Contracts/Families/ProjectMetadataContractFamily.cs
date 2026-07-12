using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_project_metadata")]
    public List<ArchitectureProjectMetadataContract> StrictProjectMetadata { get; set; } = new();

    [YamlMember(Alias = "audit_project_metadata")]
    public List<ArchitectureProjectMetadataContract> AuditProjectMetadata { get; set; } = new();
}

public sealed class ArchitectureProjectMetadataContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "projects")] public List<string> Projects { get; set; } = new();

    [YamlMember(Alias = "required_properties")]
    public Dictionary<string, string> RequiredProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [YamlMember(Alias = "forbidden_properties")]
    public Dictionary<string, string> ForbiddenProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [YamlMember(Alias = "allowed_friend_assemblies")]
    public List<string>? AllowedFriendAssemblies { get; set; }

    [YamlMember(Alias = "forbidden_project_references")]
    public List<string> ForbiddenProjectReferences { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
