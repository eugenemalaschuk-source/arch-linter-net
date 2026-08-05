## ADDED Requirements

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

