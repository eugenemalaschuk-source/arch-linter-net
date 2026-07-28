## MODIFIED Requirements

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
