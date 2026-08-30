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

    [YamlMember(Alias = "diagnostic_filter")]
    public ArchitectureExternalEvidenceDiagnosticFilter? DiagnosticFilter { get; set; }
}

/// <summary>Optional exact-match filter for diagnostics in one external-evidence requirement.</summary>
public sealed class ArchitectureExternalEvidenceDiagnosticFilter
{
    [YamlMember(Alias = "rule_ids")]
    public List<string> RuleIds { get; set; } = new();

    [YamlMember(Alias = "rule_tags")]
    public List<string> RuleTags { get; set; } = new();

    [YamlMember(Alias = "projects")]
    public List<string> Projects { get; set; } = new();

    [YamlMember(Alias = "path_prefixes")]
    public List<string> PathPrefixes { get; set; } = new();

    [YamlMember(Alias = "severity")]
    public Dictionary<string, string> Severity { get; set; } = new(StringComparer.Ordinal);

    [YamlMember(Alias = "require_matches")]
    public bool RequireMatches { get; set; }
}
