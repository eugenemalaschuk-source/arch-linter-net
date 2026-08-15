using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

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

    // Named `assembly`-kind source sets whose resolved members are unioned into
    // AllowedOnlyInAssemblies at load time, so several hosts can share one reviewed composition
    // boundary without duplicating the contract (see design.md Decision 4).
    [YamlMember(Alias = "allowed_only_in_assembly_sets")]
    public List<string> AllowedOnlyInAssemblySets { get; set; } = new();

    // Direct assembly + type identity selector — narrower than allowed_only_in_assemblies (which
    // allows every type in the named assembly) or allowed_only_in_namespaces (every type in the
    // namespace). Exists specifically so a single global/top-level type such as a host's `Program`
    // can be the composition boundary without also allowing the rest of its assembly or namespace,
    // and without requiring semantic-role classification (no attribute/CEL `when` matching) — see
    // issue #360's clarification. Matched by exact assembly name + exact fully-qualified type name.
    [YamlMember(Alias = "allowed_only_in_types")]
    public List<ArchitectureCompositionTypeSelector> AllowedOnlyInTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCompositionTypeSelector
{
    [YamlMember(Alias = "assembly")] public string Assembly { get; set; } = string.Empty;

    [YamlMember(Alias = "type")] public string Type { get; set; } = string.Empty;
}
