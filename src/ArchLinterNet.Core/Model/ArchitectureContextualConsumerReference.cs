namespace ArchLinterNet.Core.Model;

// A plain data marker recording that a contextual dependency/allow-only contract's selector
// referenced a (role, metadata key) pair directly, without an intermediate layers.<name>.selector.
// Populated eagerly on ArchitectureAnalysisSession construction (from every declared contextual
// contract, independent of --contract-id selection) rather than during checker execution, so a
// future coverage change (#114's scope: semantic_role variant) can treat this consumption
// identically to a layers.<name>.selector match regardless of the contract-family execution order,
// per semantic-classification-model's "Consumption is not hard-coded to layers.<name>.selector
// alone" requirement. MetadataKey is empty when the selector declares no metadata constraints
// (role-only consumption).
// See openspec/changes/add-contextual-dependency-contracts/design.md Decision 7.
public sealed record ArchitectureContextualConsumerReference(string Role, string MetadataKey);
