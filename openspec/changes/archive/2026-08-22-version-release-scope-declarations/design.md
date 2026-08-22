## Context

The Checkpoint B release-scope generator currently reads one fixed
`tools/release/release-scope.json` declaration. That model was adequate for one
active release but cannot distinguish the still-unpublished v0.6.4 authority
from the v0.7.0 authority. The candidate manifest is already a trusted,
immutable, workflow-pinned source of the candidate version, source commit, and
digest.

Issue #527 records that v0.6.4 publication has not been triggered. This change
therefore preserves its #527/#525/#526 declaration as a supported publication
target while adding the #613 v0.7.0 authority.

## Goals / Non-Goals

**Goals:**

- Select exactly one reviewed declaration using the candidate manifest's exact
  stable version, not a caller path, filename convention, milestone membership,
  or mutable issue text.
- Make declaration identity and bytes part of candidate-specific evidence and
  verify that relationship before Checkpoint B authorizes publication.
- Keep all unsupported mappings fail-closed and cover release-line coexistence
  and failure cases with release-tool tests.

**Non-Goals:**

- Redesign version calculation, candidate manifest semantics, Checkpoint B
  scenarios, provenance subjects, or package publication.
- Infer blockers from issue milestones or change required/excluded inventories
  owned by #527 and #613.
- Support preview or emergency-override target mappings in this release flow.

## Decisions

### Discover declarations from a fixed tracked directory

Declarations move to `tools/release/scopes/`. The generator scans only its
fixed directory, reads each declaration's explicit `release_target`, and
selects the one equal to the candidate manifest version. Filenames are merely
storage names and never determine release semantics. The command-line surface
remains limited to repository and source commit; test seams may accept paths
only because they are not release-authorizing command inputs.

This is preferred over a caller-provided path or global mutable registry because
it keeps selection within reviewed workspace inputs and lets multiple active
authorities coexist.

### Use schema v2 declarations and evidence

Each declaration carries the v2 schema identifier, a stable explicit
`declaration_id`, stable `release_target`, authority `story`, and typed
required, excluded, and delivered-context inventories. Stable `X.Y.Z` targets
are the supported mapping domain; prerelease values and unknown targets fail
closed. The evidence schema is revised to include candidate version,
declaration identity, and declaration SHA-256 alongside the existing manifest
digest, source commit, live required-item states, and excluded/delivered
inventories.

This explicit evidence revision prevents an older record from being accepted by
the final aggregator without the new binding fields.

### Revalidate selection during aggregation

The aggregator verifies the scope evidence schema, candidate manifest digest,
source commit, candidate version, release target, declaration ID, and digest
shape before evaluating open required items. A record for another source,
manifest, or target is rejected before an authorization statement can be
emitted.

### Preserve the two reviewed authorities

The v0.6.4 declaration retains #527 as authority, #525/#526 as required, and
its existing explicit exclusions. The v0.7.0 declaration uses #613, requires
#234, #116, #269, #267, and #614, explicitly excludes #287 with its release
reason, and records delivered #222 context. #614 is included because real
release authorization occurs after the implementation issue is closed; there
is no evidence-state circularity in the generator.

## Risks / Trade-offs

- [A supported maintenance target is forgotten] → its declaration remains
  tracked and coexistence tests resolve both releases independently.
- [A new version is released without a reviewed authority] → exact stable
  target matching rejects it rather than selecting a nearest declaration.
- [Declaration bytes change after review] → the SHA-256 appears in
  candidate-bound evidence and is validated by aggregation.
- [Future preview needs] → the current flow rejects it deliberately; a future
  change must add an explicit versioned mapping and tests.

## Migration Plan

1. Move the v0.6.4 declaration into the fixed scopes directory and create the
   v0.7.0 declaration.
2. Update selection, evidence, aggregation, documentation, and regressions.
3. Validate the release tools and strict OpenSpec changes.
4. Roll back by reverting this atomic change; no remote release state or
   historical evidence is rewritten.
