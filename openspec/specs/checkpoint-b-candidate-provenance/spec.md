# checkpoint-b-candidate-provenance Specification

## Purpose
Define the immutable candidate package boundary used by Checkpoint B: the same
manifested NuGet files are consumed by isolated adopters, checked on every
required platform, and re-verified immediately before publication.
## Requirements
### Requirement: One immutable candidate package set is validated and published
The release workflow SHALL resolve the release version before packing, pack one
candidate set with final release metadata, compute a manifest of package ids,
versions, sizes, and SHA-256 digests, and upload that set as an immutable
workflow artifact. Every Checkpoint B platform job and the publishing job SHALL
download that exact artifact and SHALL reject it when its manifest differs.

#### Scenario: Candidate passes Checkpoint B
- **WHEN** every required platform evidence record validates the candidate manifest
- **THEN** the publishing job re-verifies each downloaded package digest and
  publishes those exact files without running `dotnet pack` again

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
that the candidate identifies one release everywhere a consumer can observe it: the installed
CLI's reported version, the packaged README's public adoption package line, the packaged
compatibility manifest's product version, and the schema identities the installed `schema list`
advertises. A mismatch SHALL fail the gate before publication.

#### Scenario: Packaged README names a stale package line
- **WHEN** the packaged README still identifies a previous release as the public adoption package
  line
- **THEN** the release-identity scenario fails and the candidate is not authorized

#### Scenario: Registry product version differs from the candidate
- **WHEN** the packaged compatibility manifest's product version differs from the candidate's
  public adoption package line
- **THEN** the release-identity scenario fails and the candidate is not authorized

