using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

/// <summary>
/// Governs repeated feature modules discovered as immediate namespace children of one container.
/// Unlike layer-based independence, adding a new child does not require updating a peer inventory.
/// </summary>
public sealed class ArchitectureModuleContainerContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "container")] public string Container { get; set; } = string.Empty;

    [YamlMember(Alias = "profile")] public string Profile { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed_container_root_types")]
    public List<string> AllowedContainerRootTypes { get; set; } = new();

    [YamlMember(Alias = "allowed_module_root_types")]
    public List<string> AllowedModuleRootTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
