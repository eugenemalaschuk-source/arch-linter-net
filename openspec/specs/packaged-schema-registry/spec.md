# packaged-schema-registry Specification

## Purpose
TBD - created by archiving change ship-versioned-packaged-schemas. Update Purpose after archive.
## Requirements
### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest and exact 0.5.1 schema resources only for persisted formats whose writers are implemented and whose real generated output is validated: policy root v1, policy fragment v1, baseline v2 with identity version v1, public API snapshot v1, and analysis build-state receipt v1. Finding, analysis-cache, and analysis-profile schemas SHALL NOT be published in the immutable package registry until their owning slices provide implemented writers and real-output validation.

Each manifest entry SHALL contain a logical schema id, document version, packaged resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration or deprecation note, and owning OpenSpec capability.

#### Scenario: Installed package is used offline
- **WHEN** an installed CLI or Core NuGet consumer resolves its packaged schema registry without a repository checkout or network access
- **THEN** it receives the complete matching set of implemented 0.5.1 schema resources

#### Scenario: Deferred format has no writer
- **WHEN** an owning format slice has no implemented writer and generated-output validation
- **THEN** the package registry does not publish an immutable schema for that format

#### Scenario: Package resource is missing or inconsistent
- **WHEN** a manifest resource is omitted or has a mismatched digest or `$id`
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

