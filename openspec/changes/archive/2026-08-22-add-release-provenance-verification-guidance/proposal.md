## Why

The release workflow already creates and independently verifies attestations for
the frozen package and release-evidence subjects, but consumers have no
complete, rehearsed guide that starts by authenticating the manifest/checksum
evidence before trusting its package digests. The project must close that
consumer trust boundary before the first v0.7 publication and make explicit,
separate decisions on author signing and package-level SBOMs.

## What Changes

- Add an evergreen public guide for consumer verification of GitHub Release or
  rehearsal package assets, their manifest/checksum evidence, and GitHub build
  provenance in the required trust-preserving order.
- Document a separate post-publication NuGet.org verification path that uses
  package identity and repository-signature/trusted-repository semantics rather
  than false raw-SHA-256 equivalence with pre-upload package bytes.
- Record decisions to defer NuGet author signing and package-level SBOMs, with
  their operational rationale and the conditions for separate future work.
- Link the guide from public entry points and generated GitHub Release notes;
  add deterministic documentation/workflow tests to prevent trust-model drift.

## Capabilities

### New Capabilities

- `release-provenance-verification`: Consumer-facing, artifact-based release
  provenance verification guidance and explicit signing/SBOM decision record.

### Modified Capabilities

- `release-process-documentation`: Direct consumers and maintainers to the
  canonical verification guide and require the release record to surface it.
- `ci-release-gate`: Require the publish path to append a stable verification
  guide link to generated GitHub Release notes while retaining the frozen,
  attested release assets.

## Impact

Public documentation, MkDocs navigation and README entry points, release-note
assembly in the manual workflow, and focused release-documentation/workflow
tests change. Package contents, manifest semantics, attestation generation,
NuGet author signing, SBOM generation, public APIs, and CI topology do not.
