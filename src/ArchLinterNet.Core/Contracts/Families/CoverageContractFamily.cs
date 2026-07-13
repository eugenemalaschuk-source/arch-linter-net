using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    // Bound (not executed) so a schema-valid coverage contract is detected and rejected with a
    // clear "reserved, not implemented" diagnostic instead of being silently dropped by
    // IgnoreUnmatchedProperties deserialization. See ArchitecturePolicyDocumentLoader.Load.
    // The coverage engine itself is implemented by #97-#103.
    [YamlMember(Alias = "strict_coverage")]
    public List<ArchitectureCoverageContract> StrictCoverage { get; set; } = new();

    [YamlMember(Alias = "audit_coverage")]
    public List<ArchitectureCoverageContract> AuditCoverage { get; set; } = new();
}

public sealed class ArchitectureCoverageRoot
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "include")] public List<string> Include { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<string> Exclude { get; set; } = new();
}

public sealed class ArchitectureCoverageExclusion
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "project")] public string Project { get; set; } = string.Empty;

    [YamlMember(Alias = "assembly")] public string Assembly { get; set; } = string.Empty;

    [YamlMember(Alias = "contract_id")] public string ContractId { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    [YamlMember(Alias = "metadata")] public Dictionary<string, object> Metadata { get; set; } = new();

    [YamlMember(Alias = "between")] public List<string> Between { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCoverageContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "scope")] public string Scope { get; set; } = string.Empty;

    [YamlMember(Alias = "roots")] public List<ArchitectureCoverageRoot> Roots { get; set; } = new();

    [YamlMember(Alias = "between")] public List<List<string>> Between { get; set; } = new();

    [YamlMember(Alias = "contract_ids")] public List<string> ContractIds { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<ArchitectureCoverageExclusion> Exclude { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
}
