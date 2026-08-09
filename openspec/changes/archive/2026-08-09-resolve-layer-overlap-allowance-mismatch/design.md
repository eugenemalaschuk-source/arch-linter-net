## Context

`ArchitectureAnalysisSession.PolicyConsistency.cs` already reconciles one
class of legitimate layer overlap automatically: namespace-prefix containment
(`IsContainmentRelationship`), e.g. a coarse `core` layer and a nested
`core_execution` sub-layer. Anything else — including a genuinely intentional,
non-hierarchical overlap between two disjoint-purpose layers that happen to
match the same type (e.g. a cross-cutting selector-based layer over a
namespace-based one) — has no way to be acknowledged except turning off
`analysis.policy_consistency` globally, which also blinds duplicate-ID,
allow/forbid, independence, protected-importer, and unreachable-contract
detection.

## Goals / Non-Goals

**Goals:**
- Let a policy author acknowledge one specific overlapping layer pair, locally,
  without touching any other policy-consistency check.
- Make the `layer-overlap` diagnostic message describe a mechanism that
  actually exists.
- Keep the change small and additive: new optional field, new validation, one
  new exemption branch in an existing method.

**Non-Goals:**
- Redesigning layer namespace/selector matching.
- Changing `analysis.policy_consistency` semantics, its default, or any other
  policy-consistency check's behavior.
- Suppressing overlap detection globally or per-layer-family.
- Detecting or flagging unused/dead `overlaps_with` entries (unlike
  `exclude`'s unmatched-entry diagnostic) — that is additional scope beyond
  what issue #442 asks for and would need its own design (e.g. is an
  `overlaps_with` entry "unused" if the two layers never actually match the
  same type in the current scan, or only if they can never match structurally?
  Left for a future issue if it proves necessary).

## Decisions

**Field shape: `overlaps_with: [<layer-name>, ...]` on the layer, not a
top-level list of pairs.**
Alternatives considered: a top-level `analysis.acknowledged_layer_overlaps:
[[a, b], ...]` list. Rejected because it separates the acknowledgment from the
layer definitions it concerns, and this codebase's existing precedent for
"declare an allowance next to the thing it modifies" is `layers.<name>.exclude`
(also a layer-local list). Keeping it on the layer keeps review-locality: a
reader of `layers.sales_domain` sees both its scope and any overlap it's
expected to have.

**One-sided declaration is sufficient.**
Either layer in a pair may declare `overlaps_with` naming the other; the
runtime check treats the pair as acknowledged if *either* side names the
other. Alternatives considered: requiring both sides to name each other
(symmetric declaration). Rejected as unnecessary ceremony for a Minor-severity
fix — the pair is symmetric in effect (the finding already reports both names
regardless of scan order), so requiring both sides to repeat the same fact
adds no safety, only friction. This mirrors how `IsContainmentRelationship`
already treats containment as symmetric (`IsNamespaceAncestor(a,b) ||
IsNamespaceAncestor(b,a)`).

**Validation: existence + no self-reference, not "used" tracking.**
`LayerNamespacesValidator` will reject an `overlaps_with` entry that names an
undeclared layer or the layer's own name, following the exact pattern
`CoverageValidator.cs` already uses for `between` layer-name references
(`document.Layers.ContainsKey(layerName)`). It will NOT check whether the
named layer actually overlaps with anything at scan time — that would require
running the same type-scanning pass the policy-consistency check already
runs, at load time, before code is scanned, which the `policy-consistency-
checks` spec explicitly says the load-time validators must not require. An
`overlaps_with` entry naming a layer that never actually overlaps is inert,
exactly like most `exclude` entries are inert until a matching type exists —
except `exclude` already has a *separate*, scan-time `unmatched-layer-
exclusion` diagnostic for that; deliberately not adding an equivalent for
`overlaps_with` here (see Non-Goals).

**Exemption check placement: alongside `IsContainmentRelationship` in
`TryCreateLayerOverlapFinding`.**
Add `IsAcknowledgedOverlap(layerA, layerB)` returning true when either layer's
`OverlapsWith` contains the other's declared name, checked with the same
early-return shape as the containment check. No change to `FindLayerOverlaps`
/ `FindLayerOverlapsForType` — they still enumerate every matching pair;
only the per-pair decision of whether to report changes.

**Message wording.**
Old: `"Layers 'A' and 'B' both match type 'X' without an explicit documented
allowance."` New: `"Layers 'A' and 'B' both match type 'X'. Declare
'overlaps_with' on one of the layers to mark this intentional, or narrow the
namespace/selector so they no longer overlap."` This keeps the message
actionable and names the real mechanism, matching the acceptance criterion
that diagnostic wording match an actually supported configuration path.

## Risks / Trade-offs

- **Risk**: an author declares `overlaps_with` broadly (e.g. every layer names
  every other layer) to blanket-silence the check, defeating its purpose.
  → **Mitigation**: this is no different in kind from any other explicit,
  reviewable policy declaration (e.g. `ignored_violations`) — it's visible in
  the policy file, attributable to the author, and diffable in review, unlike
  the global `policy_consistency: off` escape hatch it replaces for this one
  check. Not mitigated further; matches the issue's framing that a local,
  visible allowance is strictly better than the global lever.
- **Risk**: two layers named in each other's `overlaps_with` but never
  actually overlapping is silently accepted (no "unused" diagnostic).
  → **Mitigation**: explicitly a Non-Goal (see above); consistent with
  keeping this change narrowly scoped per the issue's own non-goals.

## Migration Plan

Purely additive: existing policies with no `overlaps_with` key are byte-for-
byte unaffected (same default-empty-list pattern as `exclude`). No baseline,
cache, or schema-version bump is needed since this is a new optional key
under the existing `version: 1` document schema. No rollback concerns beyond
reverting the change.

## Open Questions

None — resolved during exploration (see proposal "Why").
