## Why

The `layer-overlap` policy-consistency diagnostic tells authors two layers
overlap "without an explicit documented allowance," but the layer schema
exposes no such per-layer allowance — the only lever is the global
`analysis.policy_consistency: warn|off`, which also suppresses five unrelated
consistency checks (duplicate contract IDs, allow/forbid conflicts,
independence conflicts, protected-importer conflicts, unreachable contracts).
An author who has one legitimate, intentional overlap cannot express it
without silencing all of those unrelated checks too. Diagnostic wording and
actual configuration surface must agree (GitHub issue #442 / story #434,
finding F8).

## What Changes

- Add an optional `overlaps_with: [<layer-name>, ...]` field to layer
  definitions. A layer names the other internal layer(s) it is intentionally
  allowed to overlap with for the same concrete type; declaring it on either
  side of a pair is sufficient to acknowledge that pair.
- Validate `overlaps_with` entries at load time: each must reference a
  declared layer name and must not self-reference.
- The `layer-overlap` policy-consistency check no longer flags a pair
  acknowledged via `overlaps_with`, alongside the existing containment-
  hierarchy and external-layer exemptions.
- Reword the `layer-overlap` finding message to name the real mechanism
  (`overlaps_with`) and the alternative of narrowing the namespaces instead
  of claiming a nonexistent allowance.
- Update `docs/reference/yaml-schema.md` and
  `docs/policy-format/layers-and-namespaces.md` to document the new field.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `layer-contracts`: adds the `overlaps_with` field to layer definitions, its
  referential-integrity validation, and its effect on layer-overlap
  reconciliation.
- `policy-consistency-checks`: the "Overlapping layer definitions are
  detected" requirement changes wording and gains an `overlaps_with`
  acknowledgment exemption alongside the existing containment/external
  exemptions.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` —
  `ArchitectureLayer` gains `OverlapsWith`.
- `src/ArchLinterNet.Core/Contracts/Validators/LayerNamespacesValidator.cs` —
  validates `overlaps_with` referential integrity.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PolicyConsistency.cs`
  — `TryCreateLayerOverlapFinding` gains the acknowledgment check and new
  message text.
- `schema/dependencies.arch.schema.json`,
  `schema/dependencies.arch.fragment.schema.json` — `$defs/layer` gains
  `overlaps_with`.
- `docs/reference/yaml-schema.md`,
  `docs/policy-format/layers-and-namespaces.md` — document the field.
- No change to `analysis.policy_consistency` semantics, defaults, or any
  other policy-consistency check. No change to layer matching/resolution
  semantics for contracts other than the `layer-overlap` diagnostic itself.
