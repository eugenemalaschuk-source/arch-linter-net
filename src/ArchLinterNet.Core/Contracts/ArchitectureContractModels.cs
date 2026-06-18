using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed class ArchitectureContractDocument
{
    [YamlMember(Alias = "version")] public int Version { get; set; }

    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "layers")] public Dictionary<string, ArchitectureLayer> Layers { get; set; } = new();

    [YamlMember(Alias = "legacy_runtime_layers")]
    public List<string> LegacyRuntimeLayers { get; set; } = new();

    [YamlMember(Alias = "analysis")] public ArchitectureAnalysisConfiguration Analysis { get; set; } = new();

    [YamlMember(Alias = "contracts")] public ArchitectureContractGroups Contracts { get; set; } = new();
}

public sealed class ArchitectureAnalysisConfiguration
{
    [YamlMember(Alias = "target_assemblies")]
    public List<string> TargetAssemblies { get; set; } = new();

    [YamlMember(Alias = "assembly_search_paths")]
    public List<string> AssemblySearchPaths { get; set; } = new();

    [YamlMember(Alias = "source_roots")]
    public List<string> SourceRoots { get; set; } = new();
}

public sealed class ArchitectureLayer
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;
}

public sealed class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict")] public List<ArchitectureDependencyContract> Strict { get; set; } = new();

    [YamlMember(Alias = "audit")] public List<ArchitectureDependencyContract> Audit { get; set; } = new();

    [YamlMember(Alias = "strict_layers")] public List<ArchitectureLayerContract> StrictLayers { get; set; } = new();

    [YamlMember(Alias = "audit_layers")] public List<ArchitectureLayerContract> AuditLayers { get; set; } = new();

    [YamlMember(Alias = "strict_allow_only")]
    public List<ArchitectureAllowOnlyContract> StrictAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_allow_only")]
    public List<ArchitectureAllowOnlyContract> AuditAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_cycles")] public List<ArchitectureCycleContract> StrictCycles { get; set; } = new();

    [YamlMember(Alias = "audit_cycles")] public List<ArchitectureCycleContract> AuditCycles { get; set; } = new();

    [YamlMember(Alias = "strict_method_body")]
    public List<ArchitectureMethodBodyContract> StrictMethodBody { get; set; } = new();

    [YamlMember(Alias = "audit_method_body")]
    public List<ArchitectureMethodBodyContract> AuditMethodBody { get; set; } = new();

    [YamlMember(Alias = "strict_asmdef")] public List<ArchitectureAsmdefContract> StrictAsmdef { get; set; } = new();

    [YamlMember(Alias = "audit_asmdef")] public List<ArchitectureAsmdefContract> AuditAsmdef { get; set; } = new();

    [YamlMember(Alias = "strict_independence")]
    public List<ArchitectureIndependenceContract> StrictIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_independence")]
    public List<ArchitectureIndependenceContract> AuditIndependence { get; set; } = new();
}

public sealed class ArchitectureDependencyContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "forbidden_legacy_runtime")]
    public bool ForbiddenLegacyRuntime { get; set; }

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureLayerContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAllowOnlyContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCycleContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureMethodBodyContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_calls")]
    public List<string> ForbiddenCalls { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAsmdefContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "source_assemblies")]
    public List<string> SourceAssemblies { get; set; } = new();

    [YamlMember(Alias = "forbidden_editor_refs")]
    public bool ForbiddenEditorRefs { get; set; }

    [YamlMember(Alias = "forbidden_asmdef_prefixes")]
    public List<string> ForbiddenAsmdefPrefixes { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureIndependenceContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureIgnoredViolation
{
    [YamlMember(Alias = "source_type")] public string SourceType { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_reference")]
    public string ForbiddenReference { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
