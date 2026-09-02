## Context

See proposal.md for motivation. Layout conventions currently evaluate source-file facts selected
by each control, so zero selected subjects have no distinct applicability meaning. Core already
has the #505/#506/#507 expected-entry, record, evaluator, completion, normalized-finding, and
baseline seams. The declared-topology evaluator is the closest precedent for bounded mapping,
exhaustive completeness, stable reason codes, and family-native evidence.

## Goals / Non-Goals

**Goals:**

- Express reviewed folder-to-convention coverage explicitly, using only source facts already
  indexed beneath configured analysis roots.
- Produce canonical per-folder controls (plus an exhaustive scope control where applicable) and
  preserve linked convention IDs and policy provenance as native evidence.
- Make audit inventory drift observable and strict inventory incompleteness fail closed through
  existing completion/output consumers.

**Non-Goals:**

- Discovering unconfigured directories, walking paths outside analysis roots, or exposing
  filesystem traversal to CEL/YAML.
- Inferring renamed folders with edit distance or using any heuristic to decide conformance.
- Replacing ordinary layout violations, source-data-unavailable diagnostics, coverage rules, or
  canonical baseline identity behavior.

## Decisions

### Add an explicit inventory control rather than changing every convention's default semantics

Add strict/audit `layout_convention_applicability` inventory declarations alongside the existing
strict/audit layout-convention lists. An inventory has an id/name, a normalized bounded scope
under `analysis.source_roots`, an `exhaustive` flag, and expected folder items that reference
existing layout-convention IDs.
Existing conventions remain unchanged when no inventory is configured. This avoids silently
turning historic nonempty/empty selector behavior into a new gate.

An item declares an exact normalized directory relative to the inventory scope and a linked
convention id. Directory facts come only from `ArchitectureSourceFileFactIndex`; path comparison
is ordinal and separator-normalized. Referencing a missing convention, duplicate inventory/item
id, malformed path, or item outside scope is invalid policy configuration, not an
unassessable assessment result.

Alternatives rejected:

- Adding a required nonempty flag to every `files_matching` selector would not express expected
  folder inventory, exhaustive mapping, or mutual-exclusivity and would break compatibility.
- A repository-wide folder scan would create noise and violate the bounded source-fact model.

### Resolve one bounded folder mapping before producing shared applicability evidence

The evaluator collects observed source-file directory subjects under the inventory scope, orders
them ordinally, and matches them against expected items. It records: missing expected folders;
zero selector matches for each linked convention; unmapped in-scope subjects only when
`exhaustive` is true; and subjects mapped to multiple distinct linked conventions. The inventory
can infer stale entries only from the already-bounded source-fact universe, and it never guesses a
replacement.

Each expected folder emits one expected applicability entry and record with a stable identity
formed from its inventory id and folder id. An exhaustive inventory also emits one scope control
for unmapped and ambiguous source subjects. Both use required membership in the mode being
evaluated; audit stays advisory because audit evidence is evaluated and rendered only in audit
mode, while strict behavior is opt-in through the corresponding strict group. Reasons use the
established missing/stale/unexpected-empty/unmapped/ambiguous reason-code vocabulary.

### Reuse the standard contract-family dispatch and projection route

Register `layout_convention_applicability` as a normal strict/audit contract family. Its handler
evaluates against the shared checker context and returns violations only when a family-native
display is required plus `ArchitectureHandlerResult` applicability entries/records. The generic
executor carries those entries to `ArchitectureApplicabilityEvaluator`; existing formatter,
SARIF, Testing, and baseline code then project the normalized applicability findings. No new
output DTO, formatter branch, or baseline matching algorithm is added.

### Preserve presentation and validation boundaries

Schema, raw-YAML, semantic validation, source locations, catalog/baseline support, and public
API snapshots change together. Tests cover direct Core evaluation plus CLI Human/JSON/SARIF and
Testing/baseline parity through existing generic projection tests. Documentation presents audit
as the initial adoption mode and strict only once the inventory is reviewed.

## Risks / Trade-offs

- [An exact folder inventory cannot suggest a likely renamed folder] → diagnostics state the
  missing item and observed bounded subjects; authors review and update policy deliberately.
- [Nested folders could make ownership unclear] → item-path and scope matching are normalized,
  ordered, and tested for exact/unmapped/multi-mapped classifications.
- [A strict inventory can introduce new unassessable exits] → opt-in semantics and audit-first
  documentation preserve existing policies and allow staged adoption.
- [Public contract changes can drift from schema or raw validation] → add YAML round-trip,
  unknown-key, public API, and composed-policy tests as one task slice.

## Migration Plan

1. Add contracts/schema/raw and semantic validation with focused parser tests.
2. Implement bounded folder classification and shared applicability records with deterministic
   Core scenarios.
3. Register the family, prove normalized output/baseline/Testing parity, and document audit-first
   usage.
4. Archive the synchronized change after validation. Rollback removes the opt-in inventory while
   leaving existing layout convention contracts and their behavior intact.
