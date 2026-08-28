## Context

The #697 snapshot fix must preserve two boundaries at once. The classification pipeline owns per-CLR-type facts and keeps separate types from separate assemblies. The snapshot projector owns logical surfaces and must collapse repeated facts after stable identity intentionally omits assembly identity. `ArchitectureChangeReports.Validate` remains the fail-closed authority for duplicate serialized entry identities.

The current broad `(Kind, Identity)` deduplication is unsafe because legacy semantic-role identity text concatenates metadata without escaping delimiters. Different metadata structures can therefore produce the same string. Removing one by its serialized identity would hide a data-model defect instead of preserving the validator's failure.

## Goals / Non-Goals

**Goals:**

- Collapse only genuinely equivalent semantic role/context observations.
- Keep a serialized identity collision between different structures visible to final snapshot validation.
- Prove the original linked-source path with distinct dynamic CLR types in separate assemblies.

**Non-Goals:**

- Redesigning or versioning existing snapshot identity encoding.
- Changing raw classification facts, their per-assembly ownership, metadata extraction, the validator, or non-semantic snapshot entries.
- Altering snapshot schema, CLI, public API, policy grammar, or report comparison.

## Decisions

1. **Deduplicate semantic role observations by structural fields, not serialized entry identity.** Equality is ordinal subject and role equality plus the complete metadata map: same count, same ordinal keys, and equal typed values. A custom hash/equality implementation keeps the operation linear and preserves metadata-order independence.

2. **Deduplicate semantic contexts independently by `(subject, metadata key, typed value)`.** Context is a projection of role metadata and deliberately has no role component, so equal contexts from distinct roles remain one logical surface. A typed structural key avoids constructing a second ambiguous serialization just to deduplicate.

3. **Leave every other entry flow and final validation unchanged.** After the two semantic projections, `ArchitectureChangeReports.Validate` still sees all projected entries. It rejects any remaining duplicate kind/identity—including a collision between distinct semantic structures—rather than silently discarding evidence.

4. **Use two regression levels.** Projector tests pin the exact delimiter collision and equivalent manual facts. An integration test creates same-full-name marker types in separate dynamic assemblies, runs the real `ArchitectureAnalysisSession` classification path, asserts its two per-assembly facts, and projects them successfully to one logical role/context pair.

## Risks / Trade-offs

- [Risk] A custom comparer could accidentally treat metadata values of distinct supported types as equal. → Mitigation: compare `object` values directly; supported values are string, bool, and decimal, whose equality remains type-sensitive.
- [Risk] Dynamic assemblies could make the integration test flaky. → Mitigation: use in-memory `Reflection.Emit` only, unique assembly names, and existing attribute-test fixtures; no file I/O or compiler process is required.
- [Risk] Existing delimiter ambiguity persists. → Mitigation: it remains intentionally fail-closed and is pinned by regression coverage; identity-format redesign is a separate compatibility decision.

## Migration Plan

No migration is required. Existing valid snapshot identities and schema remain unchanged. Repeated equivalent semantic observations now serialize successfully, while invalid collisions continue to fail rather than producing partial data.

## Open Questions

None.
