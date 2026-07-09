using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_attribute_usage")]
    public List<ArchitectureAttributeUsageContract> StrictAttributeUsage { get; set; } = new();

    [YamlMember(Alias = "audit_attribute_usage")]
    public List<ArchitectureAttributeUsageContract> AuditAttributeUsage { get; set; } = new();
}

public sealed class ArchitectureAttributeUsageContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "attributes")] public List<string> Attributes { get; set; } = new();

    [YamlMember(Alias = "attribute_prefixes")]
    public List<string> AttributePrefixes { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_layers")]
    public List<string> AllowedOnlyInLayers { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_namespaces")]
    public List<string> AllowedOnlyInNamespaces { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_projects")]
    public List<string> AllowedOnlyInProjects { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_assemblies")]
    public List<string> AllowedOnlyInAssemblies { get; set; } = new();

    [YamlMember(Alias = "forbidden_in_layers")]
    public List<string> ForbiddenInLayers { get; set; } = new();

    [YamlMember(Alias = "forbidden_in_namespaces")]
    public List<string> ForbiddenInNamespaces { get; set; } = new();

    [YamlMember(Alias = "forbidden_in_projects")]
    public List<string> ForbiddenInProjects { get; set; } = new();

    [YamlMember(Alias = "forbidden_in_assemblies")]
    public List<string> ForbiddenInAssemblies { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
