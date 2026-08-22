# checkpoint-b-candidate-provenance Specification

## Purpose
Define the immutable candidate package boundary used by Checkpoint B: the same
manifested NuGet files are consumed by isolated adopters, checked on every
required platform, and re-verified immediately before publication.
## Requirements
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

### Requirement: External consumers use an isolated package source
Checkpoint B SHALL install the CLI and build external consumers using a generated
NuGet configuration with exactly one local candidate source, an isolated global
packages directory, and isolated HTTP cache. It SHALL verify resolved
`project.assets.json` and loaded assembly/package hashes against the candidate
manifest.

#### Scenario: A public package exists with the same identity
- **WHEN** a machine has cached or configured external package sources
- **THEN** the Checkpoint B consumer resolves only the candidate package paths
  and digests from the isolated feed

### Requirement: Cancellation proof starts after validation enters work
Checkpoint B SHALL use a deterministic barrier reached from within Testing API
validation, and a deterministic observation that the CLI has read its selected
target artifact, before requesting cancellation. It SHALL prove cancellation
produces no final report output, no temporary output, and no cache entry
reusable as success.

#### Scenario: Validation is interrupted in flight
- **WHEN** the Testing API has entered its cancellation barrier or the CLI has
  read its selected target artifact, and the caller cancels
- **THEN** the CLI and Testing API report cancellation and no successful output or cache state remains

### Requirement: The candidate agrees with itself about its release identity
The packed-artifact gate SHALL verify, from the installed candidate rather than the source tree,
that explicit release/machine surfaces identify one coherent release: the installed CLI's
reported version, the packaged compatibility manifest's product version, and the schema identities
the installed `schema list` advertises. A mismatch SHALL fail the gate before publication.

The packaged README SHALL remain an evergreen product document. It SHALL expose durable product and
documentation entrypoints and SHALL NOT be treated as a release-identity authority or name the
candidate as the current/public package line.

#### Scenario: Packaged README is evergreen
- **WHEN** Checkpoint B inspects the packaged README
- **THEN** it contains the durable product positioning and canonical adoption/upgrade route without a
  current/public product-release-line assertion

#### Scenario: Registry product version differs from the candidate
- **WHEN** the packaged compatibility manifest's product version differs from the release line
  expected by the release gate
- **THEN** the release-identity scenario fails and the candidate is not authorized

#### Scenario: README reintroduces release identity
- **WHEN** the packaged README embeds the candidate or another product SemVer as its evergreen
  adoption/status identity
- **THEN** package/documentation validation fails even when the machine release identity is otherwise coherent

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
