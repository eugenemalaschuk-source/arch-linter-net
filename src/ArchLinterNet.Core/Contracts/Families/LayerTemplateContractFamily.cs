using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_layer_templates")]
    public List<ArchitectureLayerTemplateContract> StrictLayerTemplates { get; set; } = new();

    [YamlMember(Alias = "audit_layer_templates")]
    public List<ArchitectureLayerTemplateContract> AuditLayerTemplates { get; set; } = new();
}

public sealed class ArchitectureTemplateLayer
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "optional")] public bool Optional { get; set; }
}

public sealed class ArchitectureLayerTemplateContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "containers")] public List<string> Containers { get; set; } = new();

    [YamlMember(Alias = "layers")] public List<ArchitectureTemplateLayer> Layers { get; set; } = new();

    [YamlMember(Alias = "exhaustive")] public bool Exhaustive { get; set; }

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
