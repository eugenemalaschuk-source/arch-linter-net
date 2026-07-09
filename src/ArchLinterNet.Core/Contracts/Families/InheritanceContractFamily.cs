using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_inheritance")]
    public List<ArchitectureInheritanceContract> StrictInheritance { get; set; } = new();

    [YamlMember(Alias = "audit_inheritance")]
    public List<ArchitectureInheritanceContract> AuditInheritance { get; set; } = new();
}

public sealed class ArchitectureInheritanceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source_layers")]
    public List<string> SourceLayers { get; set; } = new();

    [YamlMember(Alias = "source_namespaces")]
    public List<string> SourceNamespaces { get; set; } = new();

    [YamlMember(Alias = "forbidden_base_types")]
    public List<string> ForbiddenBaseTypes { get; set; } = new();

    [YamlMember(Alias = "forbidden_base_type_prefixes")]
    public List<string> ForbiddenBaseTypePrefixes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
