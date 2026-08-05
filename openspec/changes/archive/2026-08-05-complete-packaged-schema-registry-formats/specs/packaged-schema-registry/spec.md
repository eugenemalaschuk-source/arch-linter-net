## MODIFIED Requirements

### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest and exact 0.5.1 schema resources only for persisted formats whose writers are implemented and whose real generated output is validated: policy root v1, policy fragment v1, baseline v2 with identity version v1, public API snapshot v1, normalized finding v1, analysis build-state receipt v1, analysis-cache v1, and analysis-profile v1. Future formats without implemented writers and generated-output validation SHALL NOT be published in the immutable package registry.

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

## ADDED Requirements

### Requirement: Packaged machine-readable output schemas
The immutable packaged schema registry SHALL publish the implemented `finding/v1`, `analysis-cache/v1`, and `analysis-profile/v1` JSON schemas only after output produced through their public writer or command paths validates against the exact packaged resource. Their readers SHALL reject or explicitly report unsupported future versions rather than interpreting them as v1.

#### Scenario: Packaged schemas validate generated output
- **WHEN** a finding, persisted cache entry, or profile document is generated through its implemented public path
- **THEN** it validates against the matching exact packaged schema bytes

#### Scenario: Installed package validates output offline
- **WHEN** a freshly packed CLI/Core package is installed from a local feed in an offline directory
- **THEN** schema discovery and printed-resource byte equivalence for finding, cache, and profile formats succeed without a repository checkout or network access
