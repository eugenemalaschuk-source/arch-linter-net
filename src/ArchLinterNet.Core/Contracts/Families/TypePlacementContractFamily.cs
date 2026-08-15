using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed class ArchitectureTypeMatcher
{
    [YamlMember(Alias = "name_suffix")] public string NameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "name_prefix")] public string NamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "layer")] public string Layer { get; set; } = string.Empty;

    [YamlMember(Alias = "base_type")] public string BaseType { get; set; } = string.Empty;

    [YamlMember(Alias = "implements_interface")]
    public string ImplementsInterface { get; set; } = string.Empty;

    [YamlMember(Alias = "has_attribute")] public string HasAttribute { get; set; } = string.Empty;
}

public sealed class ArchitectureTypePlacementContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "types_matching")] public ArchitectureTypeMatcher TypesMatching { get; set; } = new();

    [YamlMember(Alias = "exclude_types_matching")]
    public List<ArchitectureTypeMatcher> ExcludeTypesMatching { get; set; } = new();

    [YamlMember(Alias = "must_reside_in_layers")]
    public List<string> MustResideInLayers { get; set; } = new();

    [YamlMember(Alias = "must_reside_in_namespaces")]
    public List<string> MustResideInNamespaces { get; set; } = new();

    [YamlMember(Alias = "must_reside_in_projects")]
    public List<string> MustResideInProjects { get; set; } = new();

    [YamlMember(Alias = "must_reside_in_assemblies")]
    public List<string> MustResideInAssemblies { get; set; } = new();

    [YamlMember(Alias = "required_name_suffix")]
    public string RequiredNameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "required_name_prefix")]
    public string RequiredNamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_name_suffix")]
    public string ForbiddenNameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_name_prefix")]
    public string ForbiddenNamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
