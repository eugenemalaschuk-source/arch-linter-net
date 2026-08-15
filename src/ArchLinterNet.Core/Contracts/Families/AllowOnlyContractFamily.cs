using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed class ArchitectureAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
