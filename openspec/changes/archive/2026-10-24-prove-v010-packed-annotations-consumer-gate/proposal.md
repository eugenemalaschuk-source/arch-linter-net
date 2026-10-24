## Why

Stable v0.10 candidates add `ArchLinterNet.Annotations` to the package inventory, but Checkpoint B still loads only the four-package v2 inventory. The release gate therefore cannot prove the annotation-first experience from the exact packed candidate before v0.10.0 publication.

## What Changes

- Extend Checkpoint B to consume the five-package v3 candidate manifest, including the source-only annotations package without a symbol package, while retaining the four-package v2 path for older and preview candidates.
- Add a manifest-bound external-consumer scenario for stable 0.10.x candidates. It exercises canonical, custom-only, and mixed annotation consumers through the packed CLI and Testing APIs, and verifies package/runtime shape, semantic composition, compatibility diagnostics, policy-authority boundaries, and aligned projections.
- Require the annotation scenario in platform and final release evidence whenever the candidate inventory contains `ArchLinterNet.Annotations`.
- Update the Checkpoint B specification with the v0.10 annotation consumer-exit gate.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: stable v0.10 candidates require a packed semantic-annotation consumer matrix bound to the candidate manifest.

## Impact

The change affects the Checkpoint B NUnit harness, candidate-manifest handling, release-evidence aggregation and tests, the packed-artifact workflow, and the `checkpoint-b-release-evidence` OpenSpec capability. It does not change annotation roles, metadata semantics, package APIs, or user policy behavior.
