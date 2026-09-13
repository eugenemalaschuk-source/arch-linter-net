## ADDED Requirements

### Requirement: Transport subjects are bound to the immutable release candidate

The release workflow SHALL derive a typed transport distribution inventory from
the existing immutable NuGet candidate manifest and reviewed release inventory
for `#806`. The inventory SHALL identify the candidate version and source
commit, the candidate-manifest digest, Relay bundle archive, approved reusable
publisher workflow and action pins, compatibility metadata, and each subject's
exact filename, media kind, size, SHA-256 digest, and source/provenance
identity. The inventory SHALL include only the reviewed `#825` transport scope
and SHALL reject floating branch/latest references, unexpected files, secrets,
or incompatible CLI/Relay/protocol/schema/storage combinations.

#### Scenario: Candidate transport inventory is complete

- **WHEN** the release candidate contains the reviewed Relay source, pinned
  publisher workflow/action, configuration templates, dependency notice, and
  compatibility metadata
- **THEN** the workflow emits one deterministic transport inventory linked to
  the exact package-manifest version, source commit, and digest
- **AND** each distributable subject records its media kind, size, digest, and
  approved source identity

#### Scenario: Wrong source or compatibility is rejected

- **WHEN** a transport subject is missing, altered, sourced from a different
  commit, names an unapproved pin, or combines incompatible CLI, Relay,
  protocol, schema, or storage versions
- **THEN** candidate verification fails closed before Checkpoint B authorization
  or publication

### Requirement: Transport provenance and release attachment consume frozen bytes

The release workflow SHALL verify the exact transport subject and transport
evidence inventories after candidate creation, attest those subjects through
the existing GitHub-hosted provenance flow, independently verify every
attested transport subject before publication, and attach the same verified
bytes to the GitHub Release. It SHALL build and freeze the Relay distribution
before attestation, SHALL not regenerate or repack it afterward, SHALL keep
NuGet package/symbol authority in the existing package manifest, and SHALL not
hash a manifest recursively into itself.

#### Scenario: Candidate transport subjects pass provenance

- **WHEN** Checkpoint B accepts the package candidate and every transport
  subject matches its inventory
- **THEN** the workflow attests the exact Relay archive, workflow, action, and
  compatibility subjects plus their outer transport evidence
- **AND** publication and GitHub Release attachment use those same verified
  bytes

#### Scenario: Tampering after freeze blocks publication

- **WHEN** an archive, workflow, action, compatibility file, transport manifest,
  or derived checksums file is missing or changed after candidate creation
- **THEN** verification fails before NuGet upload or GitHub Release creation
- **AND** no replacement or regenerated subject is accepted as a recovery

