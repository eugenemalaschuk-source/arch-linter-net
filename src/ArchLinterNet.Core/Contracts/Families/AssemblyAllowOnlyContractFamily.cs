using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> StrictAssemblyAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> AuditAssemblyAllowOnly { get; set; } = new();
}

public sealed class ArchitectureAssemblyAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
