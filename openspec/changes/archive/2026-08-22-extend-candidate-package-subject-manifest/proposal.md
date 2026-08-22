## Why

The immutable Checkpoint B candidate manifest currently inventories only primary
`.nupkg` files although the release process produces and attaches corresponding
`.snupkg` symbol packages. This leaves the project-controlled pre-publication
subject inventory incomplete and makes symbols vulnerable to omission or
substitution between Checkpoint B and publication.

## What Changes

- Evolve the existing candidate manifest to a deterministic, schema-versioned
  inventory of paired primary and symbol NuGet package subjects.
- Verify exact paired inventory, identity, size, and SHA-256 digests at every
  downstream release handoff; reject missing, unexpected, duplicate, or
  modified subjects.
- Generate a deterministic human-readable checksum rendering mechanically from
  the canonical manifest, without including that rendering as a self-hashed
  manifest subject.
- Make NuGet publication and GitHub Release attachment use the same verified
  subject inventory, including symbol-package handoff validation.
- Document the pre-publication byte-identity boundary and its distinction from
  later NuGet.org repository signing.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `checkpoint-b-candidate-provenance`: expand the immutable candidate contract
  from primary packages to paired primary and symbols subjects.
- `checkpoint-b-release-evidence`: preserve the complete candidate subject
  inventory through Checkpoint B evidence and aggregation.
- `ci-release-gate`: require release workflow handoffs to consume the complete
  verified package-subject set without a rebuild.
- `release-process-documentation`: describe the canonical manifest and derived
  checksum release evidence boundary.

## Impact

Affected systems are `tools/release/package_manifest.py`, release-evidence
helpers and tests, the manual NuGet release workflow, CI candidate checks, and
release-process documentation. The manifest remains internal release evidence;
no public library API or package-versioning semantics change.
