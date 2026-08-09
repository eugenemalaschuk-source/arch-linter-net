# packaged-schema-registry Specification

## Purpose
TBD - created by archiving change ship-versioned-packaged-schemas. Update Purpose after archive.
## Requirements
### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest listing exact schema resources only for persisted formats whose writers are implemented and whose real generated output is validated: policy root v1, policy fragment v1, baseline v2 with identity version v1, public API snapshot v1, normalized finding v1, analysis build-state receipt v1, analysis-cache v1, and analysis-profile v1. Future formats without implemented writers and generated-output validation SHALL NOT be published in the immutable package registry.

Each manifest entry SHALL contain a logical schema id, document version, packaged resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration or deprecation note, and owning OpenSpec capability. Entries SHALL default to the `0.5.1` release-qualified identity; an individual entry MAY independently advance to a later release-qualified identity (e.g. `0.6.1`) when its own schema shape changes, without requiring every other entry or the manifest's own `productVersion`/`compatibilityEnvelope` to advance. An entry's prior release-qualified bytes SHALL remain preserved, byte-for-byte, in source control after the entry advances, even though the advanced entry is no longer packaged/registry-discoverable under the prior identity.

The schema registry's baseline `0.5.1` identity is an independently versioned compatibility contract and is not required to track the product package version. An individual entry's advanced release-qualified identity MAY coincide with a product package version when a maintainer deliberately chooses that alignment for a specific schema change (e.g. minting a new policy-root identity to match an upcoming release); this is a per-entry choice, not a rule that the whole registry mirrors the product version release-for-release.

#### Scenario: Installed package is used offline
- **WHEN** an installed CLI or Core NuGet consumer resolves its packaged schema registry without a repository checkout or network access
- **THEN** it receives the complete matching set of implemented schema resources at each entry's current release-qualified identity

#### Scenario: Deferred format has no writer
- **WHEN** an owning format slice has no implemented writer and generated-output validation
- **THEN** the package registry does not publish an immutable schema for that format

#### Scenario: Package resource is missing or inconsistent
- **WHEN** a manifest resource is omitted or has a mismatched digest or `$id`
- **THEN** package/schema validation fails with the affected logical schema id and expected version

#### Scenario: Product and baseline schema identities differ intentionally
- **WHEN** an adopter inspects a `0.6.0` package's version, packaged README, and offline schema list for a baseline-identity entry that has not advanced
- **THEN** the surfaces identify `0.6.0` as the product package line and `0.5.1` as that entry's immutable shipped schema contract, without implying an unsupported `schema/0.6.0` URL

#### Scenario: One entry advances independently
- **WHEN** a schema's public shape changes (e.g. policy-root gaining a new optional field) while other entries are unaffected
- **THEN** only that entry's resource path, `$id`, and digest advance to a new release-qualified identity, the unaffected entries keep their existing identity, and the entry's prior release-qualified bytes remain unchanged in source control

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

### Requirement: Packaged normalized finding schema
The immutable packaged schema registry SHALL publish the implemented versioned normalized diagnostic JSON schema only after generated JSON output validates against it.

#### Scenario: Offline schema validates generated diagnostics
- **WHEN** an installed tool lists and prints the normalized diagnostic schema offline
- **THEN** the exact packaged schema declares the supported finding schema version and validates generated diagnostic JSON

### Requirement: Packaged machine-readable output schemas
The immutable packaged schema registry SHALL publish the implemented `finding/v1`, `analysis-cache/v1`, and `analysis-profile/v1` JSON schemas only after output produced through their public writer or command paths validates against the exact packaged resource. Each descriptor's read/write support SHALL describe an implemented public contract: finding and cache readers SHALL reject or explicitly report unsupported future versions rather than interpreting them as v1; `analysis-profile` SHALL report write-only support until a public reader exists.

#### Scenario: Packaged schemas validate generated output
- **WHEN** a finding, persisted cache entry, or profile document is generated through its implemented public path
- **THEN** it validates against the matching exact packaged schema bytes

#### Scenario: Profile reader support is absent
- **WHEN** an installed consumer lists the `analysis-profile` descriptor
- **THEN** it reports write support and does not report read support

#### Scenario: Installed package validates output offline
- **WHEN** a freshly packed CLI/Core package is installed from a local feed in an offline directory
- **THEN** schema discovery and printed-resource byte equivalence for finding, cache, and profile formats succeed without a repository checkout or network access

### Requirement: Packaged release identity regression coverage
The repository SHALL validate freshly packed artifacts so that the installed CLI version, its offline schema list, the packed README, and release-facing schema guidance use a consistent product-to-registry mapping. Validation SHALL reject stale public release-target wording and every documented immutable schema URL that is absent from the package registry.

#### Scenario: Stale release target is packaged
- **WHEN** the packed README presents `0.5.1` as the `0.6.0` product release target
- **THEN** packaged-artifact validation fails before publication

#### Scenario: Documentation names an unsupported schema identifier
- **WHEN** release-facing guidance names an immutable `$schema` URL not listed by the packed registry
- **THEN** consistency validation fails with the unsupported URL

