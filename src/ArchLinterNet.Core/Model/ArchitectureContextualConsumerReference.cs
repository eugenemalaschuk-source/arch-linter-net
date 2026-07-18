using ArchLinterNet.CEL.Compilation;

namespace ArchLinterNet.Core.Model;

// A plain data marker recording that a contextual dependency/allow-only contract's selector
// referenced a complete role/metadata constraint directly, without an intermediate layers.<name>.selector.
// Populated eagerly on ArchitectureAnalysisSession construction (from every declared contextual
// contract, independent of --contract-id selection) rather than during checker execution, so a
// future coverage change (#114's scope: semantic_role variant) can treat this consumption
// identically to a layers.<name>.selector match regardless of the contract-family execution order,
// per semantic-classification-model's "Consumption is not hard-coded to layers.<name>.selector
// alone" requirement. Metadata is empty when the selector declares no metadata constraints
// (role-only consumption). Description is a deterministic key for deduplication and diagnostics.
// See openspec/changes/add-contextual-dependency-contracts/design.md Decision 7.
//
// When/CompiledWhen mirror the originating ArchitectureContextSelector's own fields (added by
// #164 — see openspec/changes/cel-selector-contextual-integration/design.md) so stale-selector
// coverage detection re-evaluates the selector's `when` expression, not just its literal
// role/metadata, when deciding whether the selector still matches any classified fact.
public sealed record ArchitectureContextualConsumerReference(
    string Role,
    IReadOnlyDictionary<string, object> Metadata,
    string Description,
    string? SourceRole = null,
    IReadOnlyDictionary<string, object>? SourceMetadata = null,
    string? When = null,
    string? SourceWhen = null)
{
    // Kept internal (unlike the positional properties above) so the compiled CEL predicate — an
    // ArchLinterNet.CEL engine type — never appears on this Core model's public surface, matching
    // ArchitectureContextSelector.CompiledWhen's own internal visibility.
    internal CelCompiledPredicate? CompiledWhen { get; init; }

    internal CelCompiledPredicate? SourceCompiledWhen { get; init; }
}
