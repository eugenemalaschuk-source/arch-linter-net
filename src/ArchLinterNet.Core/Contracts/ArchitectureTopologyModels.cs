using ArchLinterNet.Core.Resolution;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

/// <summary>Native, reviewable declaration of a bounded static architecture topology.</summary>
public sealed class ArchitectureTopology
{
    [YamlMember(Alias = "mode")] public string Mode { get; set; } = "partial";

    [YamlMember(Alias = "subject_kind")] public string SubjectKind { get; set; } = string.Empty;

    [YamlMember(Alias = "scope")] public ArchitectureTopologyScope Scope { get; set; } = new();

    [YamlMember(Alias = "nodes")] public List<ArchitectureTopologyNode> Nodes { get; set; } = new();

    [YamlMember(Alias = "allowed_edges")]
    public List<ArchitectureTopologyEdge> AllowedEdges { get; set; } = new();

    [YamlMember(Alias = "out_of_scope")]
    public List<ArchitectureTopologyOutOfScopeDeclaration> OutOfScope { get; set; } = new();

    [YamlMember(Alias = "stale_declarations")] public bool StaleDeclarations { get; set; }
}

/// <summary>Policy-owned observed universe for one declared topology.</summary>
public sealed class ArchitectureTopologyScope
{
    [YamlMember(Alias = "allow_empty")] public bool AllowEmpty { get; set; }

    [YamlMember(Alias = "selectors")]
    public List<ArchitectureTopologySubjectSelector> Selectors { get; set; } = new();
}

/// <summary>One stable component and the subject selectors that can map to it.</summary>
public sealed class ArchitectureTopologyNode
{
    [YamlMember(Alias = "id")] public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "mappings")]
    public List<ArchitectureTopologySubjectSelector> Mappings { get; set; } = new();
}

/// <summary>One directed relationship permitted between declared topology components.</summary>
public sealed class ArchitectureTopologyEdge
{
    [YamlMember(Alias = "from")] public string From { get; set; } = string.Empty;

    [YamlMember(Alias = "to")] public string To { get; set; } = string.Empty;
}

/// <summary>Reviewed, bounded exclusion from an otherwise declared topology scope.</summary>
public sealed class ArchitectureTopologyOutOfScopeDeclaration
{
    [YamlMember(Alias = "id")] public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "selector")] public ArchitectureTopologySubjectSelector Selector { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Closed selector vocabulary shared by topology scope, mappings, and reviewed exclusions. Exactly
/// one primary selector is valid: a layer, namespace pattern, project, assembly, or semantic context.
/// </summary>
public sealed class ArchitectureTopologySubjectSelector
{
    private string _namespace = string.Empty;

    [YamlMember(Alias = "layer")] public string Layer { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace")]
    public string Namespace
    {
        get => _namespace;
        set
        {
            _namespace = value;
            _cachedNamespacePattern = null;
        }
    }

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "project")] public string Project { get; set; } = string.Empty;

    [YamlMember(Alias = "assembly")] public string Assembly { get; set; } = string.Empty;

    [YamlMember(Alias = "context")] public ArchitectureContextSelector? Context { get; set; }

    [YamlIgnore] private NamespaceGlobPattern? _cachedNamespacePattern;

    [YamlIgnore]
    internal NamespaceGlobPattern NamespacePattern => _cachedNamespacePattern ??= NamespaceGlobPattern.Parse(Namespace);
}
