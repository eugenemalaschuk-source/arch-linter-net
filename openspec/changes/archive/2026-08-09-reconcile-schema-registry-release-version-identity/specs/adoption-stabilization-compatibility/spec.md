## MODIFIED Requirements

### Requirement: Release-matched compatibility registry
The system SHALL publish one `adoption-stabilization/v1` registry for 0.5.1 that identifies every shipped persisted or machine-readable contract by logical schema id, document version, packaged resource path, and compatibility behavior. The 0.5.1 registry SHALL contain exactly the formats whose owning slices implement their writers and validate real generated output:

| Surface | Logical schema/version | 0.5.1 writer behavior |
|---|---|---|
| Root policy | `policy-root/v1`, YAML `version: 1` | writes/validates v1 |
| Imported fragment | `policy-fragment/v1` | writes/validates the release-matched fragment schema |
| Baseline | `baseline/v2`, YAML `version: 2`, identity `identity_version: 1` | writes v2; reads v1 and v2 |
| Public API snapshot | `api-snapshot/v1`, document `version: 1` | writes v1 |
| Normalized finding | `finding/v1`, JSON `schema_version: 1` | writes v1; unknown schema versions fail and unknown v1 kinds follow the documented strict/non-strict rule |
| Analysis/build state | `analysis-build-state/v1` | reuses the approved fingerprint/receipt contract |
| Analysis cache | `analysis-cache/v1`, envelope format version 2 | writes and inspects verified cache entries; unsupported versions fail explicitly |
| Analysis profile | `analysis-profile/v1` | writes deterministic counters and optional measurements; the package declares write-only support until a public reader exists |
| Compatibility registry | `adoption-stabilization/v1` | writes the release-matched registry |

The 0.6.0 product package line SHALL ship and document this registry as an independently versioned immutable compatibility contract. Packaged JSON Schemas and text-format contracts SHALL use immutable release-qualified ids under `https://archlinternet.dev/schema/0.5.1/` and SHALL be shipped in the CLI and applicable NuGet packages. Unversioned web schema URLs MAY remain convenience aliases but SHALL NOT be the compatibility source of truth.

#### Scenario: Schema is consumed offline
- **WHEN** an editor, pre-commit hook, or CI job has the 0.6.0 package but no network access
- **THEN** it can discover and validate every shipped 0.5.1 document format from packaged resources and the registry

#### Scenario: Future format changes equality
- **WHEN** a future release changes a required field, equality rule, discriminated union, or canonicalization rule
- **THEN** it introduces a new logical/document version or an explicitly compatible additive extension instead of silently reinterpreting a 0.5.1 version

#### Scenario: Product package retains an unchanged compatibility registry
- **WHEN** an adopter installs the 0.6.0 CLI package
- **THEN** its README and release guidance identify the embedded 0.5.1 schema registry as intentionally independent from the package SemVer
