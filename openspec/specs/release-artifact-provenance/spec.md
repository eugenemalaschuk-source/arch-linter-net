# release-artifact-provenance Specification

## Purpose
TBD - created by archiving change attest-release-artifacts. Update Purpose after archive.
## Requirements
### Requirement: Frozen release subjects receive signed GitHub build provenance
After Checkpoint B has accepted the immutable candidate, the release workflow
SHALL re-verify its canonical v2 manifest and deterministic checksum evidence
before creating GitHub-hosted build-provenance attestations. It SHALL attest
exactly every manifest-selected pre-publication `.nupkg` and `.snupkg` subject
and, in a separate outer evidence inventory, exactly the canonical manifest and
derived checksum files. It SHALL use an immutable action commit pin and grant
only the producing job `contents: read`, `id-token: write`, and
`attestations: write` permissions.

#### Scenario: A candidate has the complete expected subject set
- **WHEN** the v2 manifest, all package/symbol files, and the canonical
  manifest/checksum evidence files agree
- **THEN** the workflow creates provenance only for those exact package and
  outer-evidence subject sets

#### Scenario: A package or outer evidence subject is invalid
- **WHEN** an expected subject is missing or altered, or an unexpected subject
  is present in either attestation inventory
- **THEN** the workflow fails before it creates an attestation or reaches a
  publication handoff

### Requirement: Provenance is independently verified before publication
The workflow SHALL use supported GitHub CLI consumer verification in a job
separate from the producing job. It SHALL verify every package, symbol,
canonical-manifest, and checksum subject against the exact repository, release
workflow identity, and source commit; fail when any expected subject lacks a
valid attestation; and prove that modified package, manifest, and checksum
bytes do not verify. This provenance verification SHALL run for a non-publishing
release rehearsal whenever GitHub attestation permissions are available.

#### Scenario: Every frozen subject verifies in a rehearsal
- **WHEN** a `publish=false` release run reaches the provenance verification job
- **THEN** it verifies every expected subject without publishing to NuGet.org or
  creating a GitHub Release

#### Scenario: A subject is modified or has no attestation
- **WHEN** consumer verification receives a tampered subject or cannot find an
  attestation for one expected subject
- **THEN** provenance verification fails and the release cannot publish or
  attach assets

### Requirement: Publication consumes attested pre-publication bytes
The NuGet upload and GitHub Release attachment steps SHALL consume the same
re-verified package, symbol, manifest, and checksum bytes that passed
provenance verification. No project-controlled step SHALL rebuild, repack,
resign, or regenerate one of those subjects after attestation. A later
NuGet.org repository-signed primary-package download SHALL be treated as a
distinct repository artifact and SHALL NOT be compared with the pre-upload
manifest by raw SHA-256 equality.

#### Scenario: A publishing release proceeds after provenance passes
- **WHEN** a `publish=true` run has passed provenance verification
- **THEN** its NuGet push and GitHub Release attachment paths use only the
  frozen manifest-selected and outer-evidence subjects
