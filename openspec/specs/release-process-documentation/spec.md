# Release Process Documentation Specification

## Purpose
Documents the manual release procedure for maintainers.
## Requirements
### Requirement: Manual release procedure documentation
ArchLinterNet SHALL document the initial preview package release process using the manual NuGet release workflow.

#### Scenario: Dry-run procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to run the manual release workflow from the GitHub Actions UI with an explicit preview version and `publish=false`

#### Scenario: Public publication procedure is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains how to rerun the manual release workflow from the GitHub Actions UI with the same explicit preview version and `publish=true`

#### Scenario: NuGet.org trusted publishing setup is documented
- **WHEN** a maintainer reads the release process documentation
- **THEN** it explains the required NuGet.org trusted publishing policy fields and states that classic API keys are not used for automated publishing

#### Scenario: Publication recordkeeping is documented
- **WHEN** packages are published publicly
- **THEN** the documentation instructs maintainers to record published package IDs, versions, and GitHub Pages deployment URL in issue or PR notes

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

### Requirement: Partial publication retries fail closed
The release-process documentation SHALL state that a duplicate primary package
push is a fail-closed release-integrity condition because it does not prove its
paired symbol package was published. It SHALL instruct maintainers to inspect
NuGet.org package and symbol state before creating a corrected release path.

#### Scenario: A maintainer handles a partial publication
- **WHEN** a release rerun encounters an existing primary package
- **THEN** the documentation directs the maintainer to stop and investigate the
  paired package/symbol state instead of relying on duplicate-success behavior
