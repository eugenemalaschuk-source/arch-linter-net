using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_port_boundaries")]
    public List<ArchitecturePortBoundaryContract> StrictPortBoundaries { get; set; } = new();

    [YamlMember(Alias = "audit_port_boundaries")]
    public List<ArchitecturePortBoundaryContract> AuditPortBoundaries { get; set; } = new();
}

/// <summary>Restricts a contextual dependency to a reviewed port or ACL seam.</summary>
public sealed class ArchitecturePortBoundaryContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;
    [YamlMember(Alias = "id")] public string? Id { get; set; }
    [YamlMember(Alias = "source")] public ArchitectureContextSelector Source { get; set; } = new();
    [YamlMember(Alias = "target_context")] public ArchitectureContextMetadataSelector TargetContext { get; set; } = new();
    [YamlMember(Alias = "allowed_seams")] public List<ArchitectureContextSelector> AllowedSeams { get; set; } = new();
    [YamlMember(Alias = "forbidden")] public List<ArchitectureContextSelector> Forbidden { get; set; } = new();
    [YamlMember(Alias = "adapter_bindings")] public List<ArchitectureAdapterPortBinding> AdapterBindings { get; set; } = new();
    [YamlMember(Alias = "exclude")] public List<ArchitectureContextSelector> Exclude { get; set; } = new();
    [YamlMember(Alias = "ignored_violations")] public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAdapterPortBinding
{
    [YamlMember(Alias = "adapter")] public ArchitectureContextSelector Adapter { get; set; } = new();
    [YamlMember(Alias = "expected_port")] public ArchitectureContextSelector ExpectedPort { get; set; } = new();
    [YamlMember(Alias = "allowed_contexts")] public List<ArchitectureContextSelector> AllowedContexts { get; set; } = new();
}

public sealed class ArchitectureContextMetadataSelector
{
    [YamlMember(Alias = "metadata")] public Dictionary<string, object> Metadata { get; set; } = new();
}
