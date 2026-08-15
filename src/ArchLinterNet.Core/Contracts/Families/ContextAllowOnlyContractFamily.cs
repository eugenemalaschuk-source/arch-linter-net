using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

// Restricts a source selector's dependencies to same-context (or explicitly allowed) targets,
// mirroring ArchitectureContextDependencyContract's selector/operator conventions. See
// openspec/changes/add-contextual-dependency-contracts/specs/contextual-allow-only-contracts/spec.md.
public sealed class ArchitectureContextAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public ArchitectureContextSelector Source { get; set; } = new();

    [YamlMember(Alias = "allowed")] public List<ArchitectureContextSelector> Allowed { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<ArchitectureContextSelector> Exclude { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
