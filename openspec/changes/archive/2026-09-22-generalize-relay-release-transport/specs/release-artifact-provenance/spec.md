## MODIFIED Requirements

### Requirement: Transport subjects are bound to the immutable release candidate

The release workflow SHALL derive a typed transport distribution inventory from
the existing immutable NuGet candidate manifest and a closed reviewed Relay
transport inventory. The transport inventory SHALL be release-line neutral: it
SHALL record the Relay support status, the historical review origin of the
bundle/publisher pins, and that current publication authority is external
Checkpoint B release-scope evidence. It SHALL NOT claim that the historical
first release authority is the current candidate's publication authority.

The generated transport manifest SHALL identify the exact candidate version and
source commit, candidate-manifest digest, Relay bundle archive, approved
reusable publisher workflow and action pins, compatibility metadata, and each
subject's exact filename, media kind, size, SHA-256 digest, and
source/provenance identity. Candidate versions SHALL support valid SemVer-style
NuGet versions across release lines, while malformed identities SHALL fail
closed.

The inventory SHALL retain only the reviewed Relay transport byte/pin surface
and SHALL reject floating branch/latest references, unexpected files, or
incompatible CLI/Relay/protocol/schema/storage combinations. Secret scanning is
outside this inventory validator's scope and remains the responsibility of
separate repository and security gates. Exact package-release authorization
SHALL remain owned by the separately verified Checkpoint B release-scope
evidence.

#### Scenario: Later release line freezes the same reviewed transport safely

- **WHEN** an immutable candidate such as `0.9.0-preview.1` contains the
  reviewed Relay source and approved pinned publisher components
- **THEN** the workflow creates one deterministic transport inventory bound to
  that exact package-manifest version, source commit, and digest
- **AND** the transport manifest records experimental support status and
  historical review origin without claiming #806 or the v0.8 lifecycle as
  current publication authority
- **AND** current package publication remains gated by the exact Checkpoint B
  release-scope declaration

#### Scenario: Candidate transport inventory is complete

- **WHEN** the release candidate contains the reviewed Relay source, pinned
  publisher workflow/action, configuration templates, dependency notice, and
  compatibility metadata
- **THEN** the workflow emits one deterministic transport inventory linked to
  the exact package-manifest version, source commit, and digest
- **AND** each distributable subject records its media kind, size, digest, and
  approved source identity

#### Scenario: Wrong source, version, or compatibility is rejected

- **WHEN** a transport subject is missing, altered, sourced from a different
  commit, uses a mismatched candidate version, names an unapproved pin, or
  combines incompatible CLI, Relay, protocol, schema, or storage versions
- **THEN** candidate verification fails closed before Checkpoint B authorization
  or publication
