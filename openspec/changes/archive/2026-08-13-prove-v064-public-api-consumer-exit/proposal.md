## Why

Issue #525 (implemented and closed via #529) let `strict_public_api_surface`/`audit_public_api_surface`
contracts select an intentional reviewed compatibility surface with `surface_selector`, instead of
forcing every exported type in a governed assembly into the snapshot. That change proved the
selector at the unit level only. It does not yet prove — from freshly packed v0.6.4 CLI/Core/Testing
artifacts, through the same install/restore path an external consumer uses — that a real modular
consumer can delete a workaround-shaped whole-assembly snapshot and keep only its intentional
compatibility surface, while CLR visibility, existing semantic roles, and exact API governance stay
unchanged. Story #527 states v0.6.4 MUST NOT be published until this gate reports PASS. Today the
packed-artifact Checkpoint B gate has no required scenario for `surface_selector` at all.

## What Changes

- Add a new synthetic `api-surface-selector` adoption fixture with a deliberately large exported
  surface (incidental implementation/domain/configuration types) alongside a small intentionally
  selected subset, selected two ways: a user-owned orthogonal `has_attribute` marker (the primary
  adoption path) and a `namespace` selector (a second bounded selector source), proving selection is
  not annotation-specific.
- Add a release-blocking `public-api-surface-selector` consumer-exit matrix to the packed-artifact
  gate, executed against the candidate tool and packages installed from the isolated local feed:
  snapshot reduction (incidental types absent from the selected snapshot, present in an
  assembly-wide sibling contract with no selector), existing semantic-role governance continuity for
  a selected `ValueObject`-role type, the exact add/remove/change delta lifecycle on a selected
  member, review-visible selector-membership changes, fail-closed behavior when a selected signature
  references an unselected first-party exported type, a green full-policy strict run, and CLI/Testing
  parity on the same effective selected surface.
- Register the new scenario IDs in the release-evidence aggregator's required consumer-cleanup
  inventory so a missing or failed scenario blocks publication, and document them in
  `docs/internal/consumer-cleanup-gate.md`.
- Advance the authoritative release-scope declaration (`tools/release/release-scope.json`) from the
  closed 0.6.1/#434 scope to the current 0.6.4/#527 scope, requiring #525 and #526 and recording the
  still-open, explicitly non-blocking milestone work (#450/#452/#453/#464 refactoring, #528 quality
  debt, #575 docs) as excluded with reasons, mirroring how the 0.6.1 gate declared its own scope.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: add the release-blocking `surface_selector` consumer-exit matrix
  as a required scenario group, alongside the existing consumer-cleanup matrix.

## Impact

- **Code**: `tests/ArchLinterNet.Core.Tests/CheckpointBReleaseGateTests.PublicApiSurfaceSelector.cs`
  and `CheckpointBReleaseGateTests.PublicApiSurfaceSelectorParity.cs` (new),
  `tests/ArchLinterNet.Core.Tests/CheckpointBReleaseGateTests.cs` (wire the new scenarios into the
  main entrypoint), new fixture directory
  `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/api-surface-selector`.
- **Release tooling**: `tools/release/aggregate_checkpoint_b_evidence.py` (required scenario set),
  `tools/release/release-scope.json` (0.6.4 scope declaration).
- **Docs**: `docs/internal/consumer-cleanup-gate.md`.
- **No product runtime code, public API, or schema format changes** — `surface_selector` itself
  already shipped in #529; this proves it from packed artifacts.
