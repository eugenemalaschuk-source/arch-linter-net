using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

/// <summary>One logical external-evidence input expected by a policy.</summary>
public sealed class ArchitectureExternalEvidenceRequirement
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "format")]
    public string Format { get; set; } = string.Empty;

    [YamlMember(Alias = "required")]
    public bool Required { get; set; }

    [YamlMember(Alias = "tool")]
    public string Tool { get; set; } = string.Empty;

    [YamlMember(Alias = "tool_version")]
    public string? ToolVersion { get; set; }

    [YamlMember(Alias = "run")]
    public string Run { get; set; } = string.Empty;

    [YamlMember(Alias = "require_repository")]
    public bool RequireRepository { get; set; }

    [YamlMember(Alias = "require_revision")]
    public bool RequireRevision { get; set; }

    [YamlMember(Alias = "require_scope")]
    public bool RequireScope { get; set; }
}
