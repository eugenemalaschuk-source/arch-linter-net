using ArchLinterNet.CEL.Compilation;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// Distinct from ArchitectureLayerSelector (Contracts/ArchitectureContractModels.cs), which remains
// pinned to exact/AND-only matching for layers.<name>.selector. This selector is used only by the
// contextual dependency/allow-only contract families and supports a broader metadata operator
// vocabulary (exact, any, in, not-equal-to-source), resolved by ArchitectureContextSelectorMatcher.
// See openspec/changes/add-contextual-dependency-contracts/design.md Decision 1.
//
// This type is also reused, unchanged in accepted keys, by ArchitecturePortBoundaryContract and
// ArchitectureAdapterPortBinding (Families/PortBoundaryContractFamily.cs). `when` is part of
// openspec/specs/cel-policy-model/spec.md's closed first-wave location list only for the
// context_dependencies/context_allow_only families - ArchitecturePolicyDocumentLoader's raw-YAML
// key validators (ValidateContextualSelectorNodeKeys/ValidateContextualSelectorListKeys) must keep
// rejecting `when` as an unknown property for port-boundary/adapter-binding selectors even though
// the property exists structurally on this shared type. See
// openspec/changes/core-cel-integration/design.md Decision D4.
public sealed class ArchitectureContextSelector
{
    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);

    [YamlMember(Alias = "when")] public string? When { get; set; }

    [YamlIgnore]
    internal CelCompiledPredicate? CompiledWhen { get; set; }

    // Populated alongside CompiledWhen by ExpressionCompilationValidator, using the same YAML path
    // it already computes for provenance (e.g. "contracts.strict_context_dependencies[0].forbidden[2]").
    // Carried on the selector itself (not recovered from ArchitecturePolicyProvenanceIndex, whose
    // "current validation subject" is a single mutable field overwritten per selector during Load
    // and unavailable by the time contract checking runs) so an evaluation-time error can name
    // exactly which selector failed, not just which type of selector.
    [YamlIgnore]
    internal string? WhenLocation { get; set; }

    // The owning contract's declared `name`, for the same reason as WhenLocation — evaluation
    // happens well after Load(), with no ArchitectureContractDependencyContract/AllowOnlyContract
    // reference in scope at the matcher, so the selector carries its own contract identity.
    [YamlIgnore]
    internal string? WhenContractName { get; set; }
}
