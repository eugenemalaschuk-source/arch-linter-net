## MODIFIED Requirements

### Requirement: Release-matched packaged schema registry
The system SHALL ship an immutable `adoption-stabilization/v1` compatibility manifest listing exact schema resources only for persisted formats whose writers are implemented and whose real generated output is validated: policy root v1, policy fragment v1, baseline v2 with identity version v1, public API snapshot v1, normalized finding v1, analysis build-state receipt v1, analysis-cache v1, and analysis-profile v1. Future formats without implemented writers and generated-output validation SHALL NOT be published in the immutable package registry.

Each manifest entry SHALL contain a logical schema id, document version, packaged resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration or deprecation note, and owning OpenSpec capability. Entries SHALL default to the `0.5.1` release-qualified identity; an individual entry MAY independently advance to a later release-qualified identity (e.g. `0.6.1`) when its own schema shape changes, without requiring every other entry or the manifest's own `productVersion`/`compatibilityEnvelope` to advance. An entry's prior release-qualified bytes SHALL remain preserved, byte-for-byte, in source control after the entry advances, even though the advanced entry is no longer packaged/registry-discoverable under the prior identity.

The `0.6.0` product package line SHALL explicitly identify the schema registry as an independently versioned compatibility contract; the schema registry version SHALL NOT be represented as the product package version.

#### Scenario: Installed package is used offline
- **WHEN** an installed CLI or Core NuGet consumer resolves its packaged schema registry without a repository checkout or network access
- **THEN** it receives the complete matching set of implemented schema resources at each entry's current release-qualified identity

#### Scenario: Deferred format has no writer
- **WHEN** an owning format slice has no implemented writer and generated-output validation
- **THEN** the package registry does not publish an immutable schema for that format

#### Scenario: Package resource is missing or inconsistent
- **WHEN** a manifest resource is omitted or has a mismatched digest or `$id`
- **THEN** package/schema validation fails with the affected logical schema id and expected version

#### Scenario: Product and schema identities differ intentionally
- **WHEN** an adopter inspects a `0.6.0` package's version, packaged README, and offline schema list
- **THEN** each surface identifies `0.6.0` as the product package line and each schema's own release-qualified identity as the immutable shipped schema contract, without implying an unsupported `schema/0.6.0` URL

#### Scenario: One entry advances independently
- **WHEN** a schema's public shape changes (e.g. policy-root gaining a new optional field) while other entries are unaffected
- **THEN** only that entry's resource path, `$id`, and digest advance to a new release-qualified identity, the unaffected entries keep their existing identity, and the entry's prior release-qualified bytes remain unchanged in source control

