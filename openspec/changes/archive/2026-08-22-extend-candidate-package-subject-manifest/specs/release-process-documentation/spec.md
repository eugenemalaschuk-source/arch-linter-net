## ADDED Requirements

### Requirement: Pre-publication package identity boundary is documented
The release-process documentation SHALL explain that the canonical candidate
manifest inventories paired `.nupkg` and `.snupkg` project-controlled
pre-publication bytes and that its deterministic checksum rendering is derived
from that inventory rather than a second authority. It SHALL distinguish this
pre-publication byte identity from NuGet.org repository signing, which can
change later downloadable `.nupkg` bytes and therefore is not verified by
pre-upload raw SHA-256 equality.

#### Scenario: A maintainer reviews release evidence
- **WHEN** a maintainer reads the release process documentation
- **THEN** they can identify the canonical package manifest, its derived
  checksum representation, and the boundary between project-controlled bytes
  and NuGet.org repository-signed downloads
