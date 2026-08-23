## MODIFIED Requirements

### Requirement: Release pipeline publishes validated immutable candidates
The manual release workflow SHALL calculate version, build final metadata, pack
and manifest the complete paired primary-package and symbol-package candidate
set once, validate the downloaded candidate set through Checkpoint B, attest
the exact frozen package and outer release-evidence subject inventories, and
independently verify every resulting GitHub build-provenance attestation before
it publishes or attaches only the same manifest-selected digest-verified
subjects. Any manifest-verification step that uses Bash environment-variable
syntax SHALL declare Bash explicitly, including Windows matrix jobs. The
workflow SHALL use exactly one primary-package NuGet push per pair, verify the
adjacent manifest-selected symbol package before the push, and SHALL fail closed
when a primary package already exists; it SHALL NOT use duplicate-success
behavior that can complete while its paired symbol package is absent. It SHALL
install the pinned OpenSpec CLI and run `openspec validate --all --strict`
before evidence aggregation. The attestation-producing job SHALL use an
immutable action commit pin and job-scoped `contents: read`, `id-token: write`,
and `attestations: write` permissions; no unrelated job SHALL receive those
attestation write permissions. The GitHub Release body SHALL append a stable
link to the canonical provenance verification guide while retaining the same
frozen package, manifest, and checksum attachment paths.

#### Scenario: Strict OpenSpec validation fails
- **WHEN** the strict OpenSpec gate fails or its pinned executable is unavailable
- **THEN** evidence aggregation and publication do not run

#### Scenario: Publication selects an unexpected package file
- **WHEN** the publication or GitHub Release attachment path selects a package
  or symbol file outside the verified manifest inventory
- **THEN** the workflow fails before publication or attachment

#### Scenario: Windows verifies the downloaded candidate
- **WHEN** a Windows Checkpoint B matrix job verifies its candidate manifest
- **THEN** the step uses Bash and passes the candidate version and source commit
  to the verifier without PowerShell interpolation

#### Scenario: A partial publish is retried
- **WHEN** a primary package already exists but its paired symbol package may be absent
- **THEN** the NuGet push step fails and does not report the release as successfully published

#### Scenario: Attestation cannot be verified independently
- **WHEN** any expected package or release-evidence subject has no valid
  repository-, workflow-, and source-commit-bound attestation
- **THEN** NuGet publication and GitHub Release attachment do not run

#### Scenario: A GitHub Release is created
- **WHEN** the `publish=true` path creates a GitHub Release from frozen attested subjects
- **THEN** its generated body contains the stable canonical verification-guide link
