## Why

The current Checkpoint B flow can validate runner-local candidate packages and
then publish a separately packed set. Its evidence therefore cannot establish
that the exact files tested on all platforms are the files sent to NuGet, and
its matrix/evidence assertions are too weak to authorize a release.

## What Changes

- Restructure the release workflow around one version-resolved, immutable
  candidate package artifact that every platform validates and the release job
  publishes after digest verification.
- Require isolated NuGet configuration and caches, package/asset provenance
  checks, real shell adapters, explicit scenario oracles, and executable cache
  and cancellation lifecycle assertions.
- Make evidence schema-backed and complete: each platform must report the exact
  scenario inventory, package manifest, observed shell, and gate results.
- Install a pinned OpenSpec CLI in the aggregation job and run strict validation.
- Replace the checked-in-evidence claim with an immutable workflow artifact
  contract, linked to the release record by digest.

## Capabilities

### New Capabilities

- `checkpoint-b-candidate-provenance`: Immutable release-candidate package
  lifecycle and platform evidence provenance.

### Modified Capabilities

- `checkpoint-b-release-evidence`: Complete required scenario matrix, strict
  evidence validation, and immutable artifact evidence storage.
- `adoption-stabilization-compatibility`: Require Checkpoint B authorization to
  cover the exact version-resolved packages proposed for publication.
- `ci-release-gate`: Require the release workflow to publish only the package
  artifact whose manifest was verified by Checkpoint B.

## Impact

The manual release workflow, Checkpoint B E2E tests, evidence aggregator,
release documentation, and OpenSpec contracts change. No product API or policy
semantics change.
