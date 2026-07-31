## Context

The baseline document already separates legacy format v1 from structured format v2, and
findings carry an `ArchitectureViolationIdentity`. The execution context presently builds a
generic identity from `source_type` and `forbidden_reference`; callers frequently omit semantic
source/target dimensions. The family registry is the exhaustive catalog of checkers, while the
normalized finding model is the public adapter seam.

## Goals / Non-Goals

**Goals:**

- Make the family registry carry an explicit identity capability/classification, so coverage is
  mechanically audited when a family is added.
- Build identities only from a family-specific semantic payload at finding creation, preserving
  contract and source-expansion provenance, source/target symbols, relevant TFM/configuration,
  and deterministic occurrence.
- Keep comparison, validation, Testing, and SARIF projections on that one identity and fail
  closed when an old structured entry is no longer exactly evaluable.

**Non-Goals:**

- Change legacy v1 matching semantics, encode source lines or display text, or automatically
  accept requalified entries.
- Redesign typed diagnostic details or add a second identity format for public-API deltas.

## Decisions

1. **Registry-owned identity inventory.** Add immutable per-family identity metadata to the
   existing registry rather than maintaining a separate hand-written inventory. The registry is
   already the ordered source of truth for registered families; a separate list could silently
   drift.
2. **Structured semantic input at the execution seam.** Replace positional optional identity
   arguments with a compact internal identity input that names source/target assembly, type,
   member, configuration, and deterministic occurrence key. This avoids making every caller
   encode semantic values into a display string. The existing positional approach is retained only
   where it is already complete until each family is converted.
3. **Exact requalification.** A structured baseline entry only matches structural equality. When
   a prior v2 entry can be related to live candidates through its legacy projection but cannot be
   mapped one-to-one, classify it as changed (or ambiguous when more than one successor exists),
   never as matched. This preserves human `reason`/`issue` metadata only for a proven one-to-one
   successor.
4. **Single identity projection.** The normalized finding, baseline lifecycle records, SARIF, and
   Testing API receive the execution identity; formatter fallbacks remain defensive-only and are
   covered by inventory tests.

## Risks / Trade-offs

- [Every checker must provide the right dimensions] → derive the required matrix from registry
  metadata and add a test that rejects generic fallback for baseline-capable families.
- [Occurrence changes after ordering changes] → compute it from a stable semantic key in the
  existing deterministic execution order, independent of output rendering and parallelism.
- [Existing v2 entries no longer match] → report them as reviewable changed/stale results and
  document explicit update-then-prune/recapture, never rewrite them during validation.

## Migration Plan

1. Run `baseline diff` or `baseline verify` after upgrading.
2. Review `changed`, `stale`, and `ambiguous` entries; they do not suppress findings.
3. Recapture exact current entries with `baseline update` or generate a reviewed replacement,
   then `baseline prune` only after the new entries are accepted.
4. Retain reasons and issue metadata only for deterministic one-to-one lineage; otherwise require
   an explicit reviewer decision. Legacy v1 baselines remain legacy until migrated explicitly.

## Open Questions

- None; the registry and normalized finding inventory supply the authoritative family list.
