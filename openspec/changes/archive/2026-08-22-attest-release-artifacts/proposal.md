## Why

The immutable candidate manifest now establishes exact pre-publication package
and checksum-evidence bytes, but consumers cannot yet verify that GitHub's
release workflow produced those bytes. This change closes that provenance gap
before the first v0.7 publication, without confusing trusted publishing,
GitHub provenance, or NuGet.org repository signatures.

## What Changes

- Attest the complete, manifest-verified `.nupkg` and `.snupkg` candidate set
  with GitHub-hosted build provenance after Checkpoint B succeeds.
- Attest the frozen canonical package manifest and derived checksum rendering
  as separate outer release-evidence subjects, avoiding recursive manifest
  identity.
- Add an independent consumer-style verification gate that verifies every
  expected subject, rejects missing/unexpected evidence, and proves modified
  package or evidence bytes do not verify.
- Require both NuGet publication and GitHub Release attachment to consume the
  same verified, attested candidate bytes without rebuilding or regenerating
  them.
- Document how to verify release assets and why a NuGet.org-downloaded primary
  package can differ after repository signing.

## Capabilities

### New Capabilities

- `release-artifact-provenance`: signed GitHub build provenance and
  consumer-style verification for the frozen pre-publication release subjects.

### Modified Capabilities

- `checkpoint-b-release-evidence`: make successful Checkpoint B a prerequisite
  for, rather than a substitute for, the subsequent provenance authority gate.
- `ci-release-gate`: require the attested exact-subject handoff before NuGet
  publication or GitHub Release attachment.
- `release-process-documentation`: provide release-asset verification guidance
  and clearly distinguish the project's pre-publication byte identity from
  NuGet.org repository signing.

## Impact

The manual NuGet release workflow, release-verification tools and their Python
tests, static workflow tests, OpenSpec contracts, and the public release
process guide change. No library public API, package contents, package-version
semantics, author signing, or SBOM behavior changes.
