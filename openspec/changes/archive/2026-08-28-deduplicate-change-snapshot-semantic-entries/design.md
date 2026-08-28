## Context

`ArchitectureChangeSnapshotProjector` receives already-produced classification facts from `ValidationOutcome`. Classification intentionally uses CLR `Type` identity, so the same internal source linked into sibling assemblies is correctly represented by multiple source facts. Snapshot role and context identities intentionally omit assembly identity to describe logical architecture surfaces. The persisted snapshot validator rejects repeated `(Kind, Identity)` values; issue #697 therefore fails at the projection boundary even though the classification layer is correct.

## Goals / Non-Goals

**Goals:**

- Persist each logical snapshot entry once, using its existing `(Kind, Identity)` key.
- Preserve deterministic snapshot ordering and distinct logical surfaces.
- Prove the behavior with a focused projector-level regression that supplies linked-marker-equivalent repeated classification facts.

**Non-Goals:**

- Changing `ArchitectureRoleIndex`, `ArchitectureClassificationAnalysisService`, type scanning, or per-assembly classification semantics.
- Changing snapshot schema, CLI commands, policy grammar, public APIs, or the existing snapshot validator's fail-closed behavior for malformed caller-supplied snapshots.
- Deduplicating findings, baseline debt, or classification facts outside the snapshot entry projection boundary.

## Decisions

1. **Deduplicate after all entries are projected and before `ArchitectureChangeSnapshot` construction.** The entry key is exactly the validator's `(Kind, Identity)` composite key. This represents repeated observations as one logical snapshot surface while keeping raw analysis output untouched. Applying the operation inside classification would incorrectly erase valid per-assembly evidence; applying it during serialization would leave the in-memory snapshot invalid and make callers observe different behavior.

2. **Retain the existing entry payload for the first deterministic projection.** Equivalent semantic role/context entries have identical display values because display is derived from the stable identity fields. The code will use the existing entry sequence and a stable duplicate-elimination operation, then rely on the report serializer's established kind/identity ordering. A custom merge model is unnecessary.

3. **Use one focused projector test for the linked-marker-equivalent input.** The projector boundary accepts classification facts rather than CLR types, so the regression will model two facts with the same stable subject/role/metadata, as produced when separate linked internal types lose assembly identity during fact projection. It will assert no snapshot exception, one role entry, one context entry, retained distinct entries, and unchanged source fact collection.

## Risks / Trade-offs

- [Risk] A broad deduplication could conceal an identity-design defect in an unrelated entry family. → Mitigation: keep the existing `(Kind, Identity)` key, assert distinct kind/identity surfaces remain present, and limit the regression to the known repeated semantic facts.
- [Risk] Moving deduplication earlier would break downstream consumers that need per-assembly facts. → Mitigation: do not change classification/index layers; deduplicate only in snapshot projection.
- [Risk] Non-deterministic duplicate selection could alter display text. → Mitigation: the duplicated facts are required to have the same logical identity and derived display; ordering remains canonical at serialization.

## Migration Plan

No migration is required. The snapshot schema and identities of distinct surfaces remain unchanged. On upgrade, snapshots that previously failed for repeated equivalent semantic facts are emitted with one logical entry per identity; existing snapshots remain readable and comparable.

## Open Questions

None.
