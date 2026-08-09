## MODIFIED Requirements

### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest and exact 0.5.1 schema resources only for persisted formats whose writers are implemented and whose real generated output is validated: policy root v1, policy fragment v1, baseline v2 with identity version v1, public API snapshot v1, normalized finding v1, analysis build-state receipt v1, analysis-cache v1, and analysis-profile v1. Future formats without implemented writers and generated-output validation SHALL NOT be published in the immutable package registry.

The `0.6.0` product package line SHALL explicitly identify the 0.5.1 registry as an independently versioned compatibility contract; the schema registry version SHALL NOT be represented as the product package version. Each manifest entry SHALL contain a logical schema id, document version, packaged resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration or deprecation note, and owning OpenSpec capability.

#### Scenario: Installed package is used offline
- **WHEN** an installed CLI or Core NuGet consumer resolves its packaged schema registry without a repository checkout or network access
- **THEN** it receives the complete matching set of implemented 0.5.1 schema resources

#### Scenario: Deferred format has no writer
- **WHEN** an owning format slice has no implemented writer and generated-output validation
- **THEN** the package registry does not publish an immutable schema for that format

#### Scenario: Package resource is missing or inconsistent
- **WHEN** a manifest resource is omitted or has a mismatched digest or `$id`
- **THEN** package/schema validation fails with the affected logical schema id and expected version

#### Scenario: Product and schema identities differ intentionally
- **WHEN** an adopter inspects a `0.6.0` package's version, packaged README, and offline schema list
- **THEN** each surface identifies `0.6.0` as the product package line and `0.5.1` as the immutable shipped schema-registry identity without implying an unsupported `schema/0.6.0` URL

## ADDED Requirements

### Requirement: Packaged release identity regression coverage
The repository SHALL validate freshly packed artifacts so that the installed CLI version, its offline schema list, the packed README, and release-facing schema guidance use a consistent product-to-registry mapping. Validation SHALL reject stale public release-target wording and every documented immutable schema URL that is absent from the package registry.

#### Scenario: Stale release target is packaged
- **WHEN** the packed README presents `0.5.1` as the `0.6.0` product release target
- **THEN** packaged-artifact validation fails before publication

#### Scenario: Documentation names an unsupported schema identifier
- **WHEN** release-facing guidance names an immutable `$schema` URL not listed by the packed registry
- **THEN** consistency validation fails with the unsupported URL
