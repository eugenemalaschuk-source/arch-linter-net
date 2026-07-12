using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_assembly_independence")]
    public List<ArchitectureAssemblyIndependenceContract> StrictAssemblyIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_independence")]
    public List<ArchitectureAssemblyIndependenceContract> AuditAssemblyIndependence { get; set; } = new();
}

public sealed class ArchitectureAssemblyIndependenceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "assemblies")] public List<string> Assemblies { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
