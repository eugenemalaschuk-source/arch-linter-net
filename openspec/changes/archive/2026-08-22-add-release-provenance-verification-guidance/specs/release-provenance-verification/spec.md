## ADDED Requirements

### Requirement: Consumers can verify GitHub project-controlled release assets in trust order
The public verification guide SHALL provide an artifact-based GitHub
Release/rehearsal path that first verifies GitHub attestations for the canonical
manifest and derived checksum evidence, then uses the verified manifest to
validate the expected package ID, version, filename, and SHA-256 inventory,
computes and compares the SHA-256 of every project-controlled `.nupkg` and
`.snupkg`, and independently verifies every package or symbol attestation
against the project repository, release workflow, and source commit.

#### Scenario: A rehearsal consumer verifies every frozen subject
- **WHEN** a consumer obtains the rehearsal or GitHub Release package subjects,
  canonical manifest, and checksum evidence
- **THEN** the documented commands verify the outer evidence before package
  digest use and verify every package and symbol subject independently

#### Scenario: A project-controlled subject is changed or lacks provenance
- **WHEN** a package, symbol, manifest, or checksum subject is modified or
  lacks its expected GitHub attestation
- **THEN** the documented verification path identifies the failed SHA-256 or
  attestation check and does not treat the release evidence as trusted

### Requirement: NuGet.org consumers use repository-aware verification
The guide SHALL provide a separate post-publication path for a
NuGet.org-downloaded primary package that checks expected package ID, version,
and source and uses supported NuGet repository-signature/trusted-repository
semantics. It SHALL state that repository signing changes downloadable raw
`.nupkg` bytes, prohibit comparison with the pre-upload SHA-256 inventory, and
make no unverified primary-package signing or raw-byte claim for `.snupkg`.

#### Scenario: A consumer verifies a NuGet.org package
- **WHEN** a consumer obtains a primary package from NuGet.org
- **THEN** the guide directs them to repository-signature and package-identity
  verification rather than a false pre-upload raw-byte equality check

### Requirement: Author-signing and package-SBOM decisions are explicit
The guide SHALL record that the project defers NuGet author signing and
package-level SBOM generation. It SHALL explain that GitHub provenance and
NuGet.org repository signing are not author signatures, and that each deferred
capability requires a separate scoped implementation issue before any related
public claim.

#### Scenario: A consumer examines project signing and SBOM claims
- **WHEN** a consumer reads the verification guide
- **THEN** they can identify the deferred decision, rationale, and distinct
  future prerequisites for author signing and package-level SBOMs

### Requirement: Verification guidance is a public evergreen entry point
The canonical verification guide SHALL be included in public documentation
navigation and linked from README and release-process entry points. It SHALL
use only public rehearsal or release artifact examples and SHALL not claim a
formal SLSA level or add a provenance badge.

#### Scenario: A consumer follows a public entry point
- **WHEN** a consumer starts from the README or public documentation navigation
- **THEN** they can reach the canonical verification guide without relying on
  private repository data or a release-specific documentation page
