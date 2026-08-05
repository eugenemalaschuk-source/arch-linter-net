## Context

The #411 audit exposed two drifted specifications inherited from a previously
archived implementation: snapshot setup still describes eager CLR loading, and
final profile evidence promises a package identity while recording only a Debug
binary. A stale diagnostic purpose and parent-story queue sentence compound the
misleading audit conclusion.

## Goals / Non-Goals

**Goals:**

- Make the snapshot specification describe the actual metadata-only preparation
  and lazy runner materialization boundary consistently.
- Make package provenance in generated post-optimization evidence concrete,
  cryptographically identified, and distinct from the executed Debug DLL.
- Correct stale ownership/status wording before reissuing the audit conclusion.

**Non-Goals:**

- Change cache eligibility, validation semantics, package publication, or the
  #366 packed-artifact release gate.

## Decisions

1. Modify the earlier `analysis-snapshot` requirements rather than relying on
   the later additive requirement. This leaves one normative model for
   `CreateSnapshot`, `Evaluate`, and `AssemblyLoads`.
2. Keep both identities in benchmark evidence. The Debug DLL identifies the
   executable that was measured; the explicitly packed CLI `.nupkg` identifies
   the package built from the same source/configuration. Neither is substituted
   for the other.
3. Generate the checked-in JSON/Markdown through the explicit benchmark harness
   after packing, instead of hand-editing measurements or digests.

## Risks / Trade-offs

- [Manual benchmark is hardware-sensitive and slow] → retain explicit-test
  status and record environment/configuration rather than claiming universal
  performance.
- [A package can be stale relative to the executable] → require the harness to
  select exactly one CLI package with the current package version and record
  both identities separately.

## Migration Plan

No consumer migration is required. Archive the corrective OpenSpec change only
after regenerated evidence, strict OpenSpec validation, formatting, and full
acceptance pass.

## Open Questions

None.
