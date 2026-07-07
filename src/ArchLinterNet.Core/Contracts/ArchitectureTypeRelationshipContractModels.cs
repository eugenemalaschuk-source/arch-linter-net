using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

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

public sealed class ArchitectureInterfaceImplementationContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "interfaces")] public List<string> Interfaces { get; set; } = new();

    [YamlMember(Alias = "interface_prefixes")]
    public List<string> InterfacePrefixes { get; set; } = new();

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

public sealed class ArchitectureCompositionContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "forbidden_apis")] public List<string> ForbiddenApis { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_layers")]
    public List<string> AllowedOnlyInLayers { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_namespaces")]
    public List<string> AllowedOnlyInNamespaces { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_projects")]
    public List<string> AllowedOnlyInProjects { get; set; } = new();

    [YamlMember(Alias = "allowed_only_in_assemblies")]
    public List<string> AllowedOnlyInAssemblies { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
