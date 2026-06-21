using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed record ArchitectureBaselineCandidate(
    string ContractGroup,
    string? ContractId,
    string SourceType,
    string ForbiddenReference);

public sealed class ArchitectureBaselineDocument
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; }

    [YamlMember(Alias = "baseline")]
    public ArchitectureBaselineContractGroups Baseline { get; set; } = new();
}

public sealed class ArchitectureBaselineContractGroups
{
    [YamlMember(Alias = "strict")] public List<ArchitectureBaselineContractEntry> Strict { get; set; } = new();
    [YamlMember(Alias = "audit")] public List<ArchitectureBaselineContractEntry> Audit { get; set; } = new();
    [YamlMember(Alias = "strict_layers")] public List<ArchitectureBaselineContractEntry> StrictLayers { get; set; } = new();
    [YamlMember(Alias = "audit_layers")] public List<ArchitectureBaselineContractEntry> AuditLayers { get; set; } = new();
    [YamlMember(Alias = "strict_allow_only")] public List<ArchitectureBaselineContractEntry> StrictAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_allow_only")] public List<ArchitectureBaselineContractEntry> AuditAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_cycles")] public List<ArchitectureBaselineContractEntry> StrictCycles { get; set; } = new();
    [YamlMember(Alias = "audit_cycles")] public List<ArchitectureBaselineContractEntry> AuditCycles { get; set; } = new();
    [YamlMember(Alias = "strict_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> StrictAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "audit_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> AuditAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "strict_method_body")] public List<ArchitectureBaselineContractEntry> StrictMethodBody { get; set; } = new();
    [YamlMember(Alias = "audit_method_body")] public List<ArchitectureBaselineContractEntry> AuditMethodBody { get; set; } = new();
    [YamlMember(Alias = "strict_independence")] public List<ArchitectureBaselineContractEntry> StrictIndependence { get; set; } = new();
    [YamlMember(Alias = "audit_independence")] public List<ArchitectureBaselineContractEntry> AuditIndependence { get; set; } = new();
    [YamlMember(Alias = "strict_protected")] public List<ArchitectureBaselineContractEntry> StrictProtected { get; set; } = new();
    [YamlMember(Alias = "audit_protected")] public List<ArchitectureBaselineContractEntry> AuditProtected { get; set; } = new();
    [YamlMember(Alias = "strict_external")] public List<ArchitectureBaselineContractEntry> StrictExternal { get; set; } = new();
    [YamlMember(Alias = "audit_external")] public List<ArchitectureBaselineContractEntry> AuditExternal { get; set; } = new();
}

public sealed class ArchitectureBaselineContractEntry
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
}
