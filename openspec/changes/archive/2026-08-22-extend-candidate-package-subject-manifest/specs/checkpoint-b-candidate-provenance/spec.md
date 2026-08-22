## MODIFIED Requirements

### Requirement: One immutable candidate package set is validated and published
The release workflow SHALL resolve the release version before packing, pack one
candidate set with final release metadata, and create a deterministic,
schema-versioned canonical manifest for every expected project-controlled
pre-publication subject. For every shipped package ID, the manifest SHALL bind
exactly one `.nupkg` primary package and exactly one corresponding `.snupkg`
symbol package as an explicit pair; every subject SHALL record its exact
filename, kind, package ID, version, byte size, SHA-256 digest, source commit,
and manifest schema version. The workflow artifact SHALL contain that complete
candidate set and manifest. Every Checkpoint B platform job and publishing job
SHALL download and re-verify that exact artifact and reject missing,
unexpected, duplicated, mismatched, or modified package/symbol subjects.

#### Scenario: Candidate passes Checkpoint B
- **WHEN** every required platform evidence record validates the candidate manifest
- **THEN** the publishing job re-verifies every paired downloaded package and
  symbol subject and publishes the manifest-selected primary package files
  without running `dotnet pack` again

#### Scenario: A symbol subject is missing or substituted
- **WHEN** an expected `.snupkg` is missing, does not match its paired package
  ID/version, or differs in size or digest from the manifest
- **THEN** candidate verification fails before Checkpoint B authorization or
  publication

## ADDED Requirements

### Requirement: Candidate checksum evidence is derived without recursive identity
The release workflow SHALL generate a deterministic human-readable checksum
representation mechanically from the validated canonical package-subject
manifest. The rendering SHALL include every `.nupkg` and `.snupkg` subject in
canonical order with its SHA-256 digest, and SHALL not be a manifest subject or
an independently maintained checksum authority.

#### Scenario: Identical candidates render identical checksum evidence
- **WHEN** the same canonical package manifest is rendered twice
- **THEN** both checksum representations have identical bytes and include every
  manifest-selected package and symbol subject

#### Scenario: A checksum file is attached as release evidence
- **WHEN** a later release-evidence process consumes the checksum representation
- **THEN** it treats the file as a derived outer evidence subject and does not
  insert its own digest into the canonical package manifest
