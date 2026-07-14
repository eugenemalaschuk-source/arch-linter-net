using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_layers")] public List<ArchitectureLayerContract> StrictLayers { get; set; } = new();

    [YamlMember(Alias = "audit_layers")] public List<ArchitectureLayerContract> AuditLayers { get; set; } = new();
}

public sealed class ArchitectureLayerContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlIgnore] public HashSet<string> OptionalLayers { get; set; } = new(StringComparer.Ordinal);

    [YamlIgnore] public string? TemplateName { get; init; }

    [YamlIgnore] public string? TemplateOwnerId { get; init; }

    [YamlIgnore] public string? ContainerNamespace { get; init; }

    [YamlIgnore] public bool Exhaustive { get; init; }

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
