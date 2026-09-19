## ADDED Requirements

### Requirement: Trusted CLI exposes temporal publication revalidation
The CLI SHALL expose `health revalidate-publication` as a read-only operation over one canonical `architecture-health/v1` JSON artifact. It SHALL accept the Health artifact, an explicit UTC evaluation date, and the source Health, payload, merged-tree, and producer-identity bindings; it SHALL write only the versioned temporal receipt to stdout or the requested output path. The operation SHALL use Core temporal semantics, SHALL not run architecture analysis or parse policy/assemblies, and SHALL return a non-ready exit result for invalid, incomplete, expired, stale, invalid, required-external, or mismatched evidence.

#### Scenario: Valid cross-midnight input produces a ready receipt
- **WHEN** the command receives a complete canonical Health evidence artifact and matching bindings for an exact merged tree
- **AND** the supplied UTC evaluation date leaves all required temporal evidence assessable
- **THEN** it emits `architecture-health-temporal-publication-receipt/v1` with state `ready`, a finite horizon, and all binding digests
- **AND** it leaves the input artifact unchanged

#### Scenario: Revalidation never reruns architecture analysis
- **WHEN** the command receives a valid serialized Health artifact
- **THEN** it reads only the serialized canonical evidence needed for temporal validation
- **AND** it does not require a policy file, solution, build, assembly load, or consumer checkout

#### Scenario: Invalid or expired input fails closed
- **WHEN** the input envelope or any supplied binding is malformed, inconsistent, stale, invalid, expired, or not finitely reusable
- **THEN** the command emits an unassessable receipt or fixed diagnostic without a ready horizon
- **AND** its exit status is non-success
