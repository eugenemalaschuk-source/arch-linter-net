## MODIFIED Requirements

### Requirement: Pre-publication package identity boundary is documented
The release-process documentation SHALL explain that the canonical candidate
manifest inventories paired `.nupkg` and `.snupkg` project-controlled
pre-publication bytes and that its deterministic checksum rendering is derived
from that inventory rather than a second authority. It SHALL explain that GitHub
build provenance attests every package/symbol subject and the canonical
manifest/checksum files as separate outer evidence subjects, and provide
consumer verification guidance that binds an asset to the repository, release
workflow, and source commit. It SHALL distinguish this pre-publication byte
identity from NuGet.org repository signing, which can change later downloadable
`.nupkg` bytes and therefore is not verified by pre-upload raw SHA-256 equality.

#### Scenario: A maintainer reviews release evidence
- **WHEN** a maintainer reads the release process documentation
- **THEN** they can identify the canonical package manifest, its derived
  checksum representation, the GitHub provenance verification procedure, and
  the boundary between project-controlled bytes and NuGet.org
  repository-signed downloads

## ADDED Requirements

### Requirement: Post-publication verification boundaries are documented
The release-process documentation SHALL state that GitHub Release attachments
are expected to retain the attested project-controlled bytes, while a
NuGet.org-downloaded primary package MUST be verified through NuGet repository
signature/trusted-repository semantics and package ID/version. It SHALL not
assert equivalent symbol-server byte or signature behavior without documented
NuGet.org symbol-service evidence, and SHALL not claim a formal SLSA level from
attestations alone.

#### Scenario: A consumer verifies a published package
- **WHEN** a consumer follows the public release verification guidance
- **THEN** it directs them to verify GitHub Release assets with GitHub
  attestations and treats the NuGet.org primary-package repository-signing
  boundary separately
