using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// The authored, YAML-bound `source_sets.<name>` declaration. Its resolved form
// (ArchitectureSourceExpansionInventory and friends) lives in Core.Model so Reporting and the CLI
// can project it without depending on the policy document model.
public sealed class ArchitectureSourceSet
{
    [YamlMember(Alias = "kind")]
    public ArchitectureSourceSetKind Kind { get; set; } = ArchitectureSourceSetKind.Assembly;

    [YamlMember(Alias = "members")] public List<string> Members { get; set; } = new();

    [YamlMember(Alias = "globs")] public List<string> Globs { get; set; } = new();

    // Opt-in, narrow exception to fail-closed zero-match expansion, mirroring the exact-identity
    // plus mandatory-reason rule optional rule inputs use (see
    // openspec/specs/optional-rule-input-coverage).
    [YamlMember(Alias = "optional")] public bool Optional { get; set; }

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
