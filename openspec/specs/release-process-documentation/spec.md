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
from that inventory rather than a second authority. It SHALL explain that GitHub
build provenance attests every package/symbol subject and the canonical
manifest/checksum files as separate outer evidence subjects, and link to the
canonical consumer verification guide that binds an asset to the repository,
release workflow, and source commit. It SHALL distinguish this pre-publication
byte identity from NuGet.org repository signing, which can change later
downloadable `.nupkg` bytes and therefore is not verified by pre-upload raw
SHA-256 equality.

#### Scenario: A maintainer reviews release evidence

- **WHEN** a maintainer reads the release process documentation
- **THEN** they can identify the canonical package manifest, its derived
  checksum representation, the canonical GitHub provenance verification guide,
  and the boundary between project-controlled bytes and NuGet.org
  repository-signed downloads

### Requirement: Partial publication retries fail closed

The release-process documentation SHALL state that a duplicate primary package
push is a fail-closed release-integrity condition because it does not prove its
paired symbol package was published. It SHALL instruct maintainers to inspect
NuGet.org package and symbol state before creating a corrected release path.

#### Scenario: A maintainer handles a partial publication

- **WHEN** a release rerun encounters an existing primary package
- **THEN** the documentation directs the maintainer to stop and investigate the
  paired package/symbol state instead of relying on duplicate-success behavior

### Requirement: Post-publication verification boundaries are documented

The release-process documentation SHALL state that GitHub Release attachments
are expected to retain the attested project-controlled bytes, while a
NuGet.org-downloaded primary package MUST be verified through NuGet repository
signature/trusted-repository semantics and package ID/version. It SHALL link to
the canonical guide's distinct NuGet author-signing and package-level SBOM
decisions, SHALL not assert equivalent symbol-server byte or signature behavior
without documented NuGet.org symbol-service evidence, and SHALL not claim a
formal SLSA level from attestations alone.

#### Scenario: A consumer verifies a published package

- **WHEN** a consumer follows the public release verification guidance
- **THEN** it directs them to the canonical guide for GitHub Release asset
  attestations and treats the NuGet.org primary-package repository-signing
  boundary separately

### Requirement: Release history-forensics procedure is documented

The maintainer release-process documentation SHALL describe the candidate-bound history-forensics job, stable and prerelease range semantics, the typed no-predecessor result, dry-run workflow artifacts, durable GitHub Release assets, candidate identity and digest verification, and a command for reproducing the analysis from the packed candidate package. The documentation SHALL state that accepted `alpha`, `beta`, `rc`, and `preview` versions share a prerelease series for the same `X.Y.Z` line, that SemVer prerelease precedence selects the prior boundary, and that build metadata does not affect ordering.

#### Scenario: Maintainer reviews a dry-run bundle

- **WHEN** a maintainer follows the release-process documentation for a dry run
- **THEN** they can locate the JSON, Markdown, manifest, checksums, and operational observations in the workflow run and understand which fields establish candidate identity

#### Scenario: Maintainer reproduces release history evidence

- **WHEN** a maintainer follows the documented reproduction procedure
- **THEN** they use the exact candidate CLI package and pinned full-SHA range with the supported one-analysis JSON/Markdown command
- **AND** they can distinguish analyzer phase measurements from orchestration observations

#### Scenario: Maintainer understands prerelease range selection

- **WHEN** a maintainer prepares an accepted `alpha`, `beta`, `rc`, or `preview` candidate
- **THEN** the documentation explains how the same `X.Y.Z` prerelease line and SemVer precedence select its history boundary
- **AND** the documentation explains that build metadata identifies the candidate but does not alter version ordering
