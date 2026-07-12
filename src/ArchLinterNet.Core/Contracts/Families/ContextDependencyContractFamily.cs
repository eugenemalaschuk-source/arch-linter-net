using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_context_dependencies")]
    public List<ArchitectureContextDependencyContract> StrictContextDependencies { get; set; } = new();

    [YamlMember(Alias = "audit_context_dependencies")]
    public List<ArchitectureContextDependencyContract> AuditContextDependencies { get; set; } = new();
}

// Compares discovered role/metadata directly between a source selector and forbidden/exclude
// selectors, without an intermediate layers.<name> declaration. See
// openspec/changes/add-contextual-dependency-contracts/specs/contextual-dependency-contracts/spec.md.
public sealed class ArchitectureContextDependencyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public ArchitectureContextSelector Source { get; set; } = new();

    [YamlMember(Alias = "forbidden")] public List<ArchitectureContextSelector> Forbidden { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<ArchitectureContextSelector> Exclude { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
