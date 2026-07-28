# packaged-schema-registry Specification

## Purpose
TBD - created by archiving change ship-versioned-packaged-schemas. Update Purpose after archive.
## Requirements
### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest and the exact 0.5.1 schema resources for policy root v1, policy fragment v1, baseline v2 with identity version v1, API snapshot v1, normalized finding v1, analysis build state v1, analysis cache v1, and analysis profile v1 in the Core and CLI release packages.

Each manifest entry SHALL contain a logical schema id, document version, packaged resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration or deprecation note, and owning OpenSpec capability.

#### Scenario: Installed package is used offline
- **WHEN** an installed CLI or Core NuGet consumer resolves its packaged schema registry without a repository checkout or network access
- **THEN** it receives the complete matching 0.5.1 manifest and exact schema resources

#### Scenario: Package resource is missing or inconsistent
- **WHEN** a manifest resource is omitted, has a mismatched digest, `$id`, or document version
- **THEN** package/schema validation fails with the affected logical schema id and expected version

### Requirement: Offline schema discovery
The CLI SHALL provide documented commands to list the packaged registry and print one named exact packaged schema without network, repository, restore, build, or target-assembly access. Listing SHALL be deterministic and identify each format version; printing an unknown schema id SHALL return a usage error.

#### Scenario: User lists schemas from an installed tool
- **WHEN** the user runs the documented schema list command in an offline directory that has no repository checkout
- **THEN** the CLI prints every logical schema id, document version, release-qualified `$id`, and packaged resource path in ordinal order

#### Scenario: User prints a named schema
- **WHEN** the user runs the documented schema print command for a listed logical schema id
- **THEN** the CLI writes the exact matching packaged schema bytes to standard output

### Requirement: Source, package, documentation, and capability consistency
The repository SHALL validate that every registry entry agrees with its source schema, embedded/package resource, public schema documentation, capability manifest, and release notes. Public editor examples SHALL use immutable release-qualified schema identifiers rather than mutable default-branch URLs as their release contract.

#### Scenario: Documentation omits a supported schema version
- **WHEN** a documented schema list or capability manifest does not identify a supported registry format and version
- **THEN** repository consistency validation fails and names the missing logical schema id

