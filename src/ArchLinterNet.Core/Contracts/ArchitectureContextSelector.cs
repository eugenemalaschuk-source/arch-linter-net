using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// Distinct from ArchitectureLayerSelector (Contracts/ArchitectureContractModels.cs), which remains
// pinned to exact/AND-only matching for layers.<name>.selector. This selector is used only by the
// contextual dependency/allow-only contract families and supports a broader metadata operator
// vocabulary (exact, any, in, not-equal-to-source), resolved by ArchitectureContextSelectorMatcher.
// See openspec/changes/add-contextual-dependency-contracts/design.md Decision 1.
public sealed class ArchitectureContextSelector
{
    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);
}
