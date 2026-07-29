## ADDED Requirements

### Requirement: Versioned normalized finding envelope
The system SHALL normalize every public diagnostic into a versioned finding envelope containing a schema version, stable lower-snake-case kind, mode/severity, message code, contract and policy origin, canonical identity, source location when available, baseline lifecycle state when applicable, and typed details.

#### Scenario: A package violation is normalized
- **WHEN** a package dependency violation is produced
- **THEN** its normalized finding has a supported schema version, `kind` of `package_dependency`, and typed project, package, condition, target-framework, and provenance evidence without parsing display text

### Requirement: Details are discriminated typed records
The system SHALL expose a typed details record for every supported finding family and SHALL NOT expose a universal untyped property bag as the public model.

#### Scenario: Different public API deltas remain distinct
- **WHEN** public API comparison produces an addition and a removal
- **THEN** the normalized findings carry different typed delta detail records identifying the respective change kinds

### Requirement: Forward compatibility is explicit
The system SHALL reject unsupported schema versions and SHALL preserve the envelope and raw details for an unknown kind in non-strict consumption while strict contract validation fails deterministically.

#### Scenario: Unknown kind has a defined outcome
- **WHEN** a v1 finding has an unrecognized kind
- **THEN** a non-strict reader exposes an opaque finding and a strict reader reports an unsupported-kind error without interpreting display text

### Requirement: Normalization fixes identity and ordering before projection
The normalized finding mapper SHALL use canonical identity and ordinal ordering independent of serialization format and SHALL NOT recompute identity from formatted messages.

#### Scenario: Same-named global programs remain distinct
- **WHEN** two findings have the same type name but different assembly/member identity
- **THEN** their normalized canonical identities and deterministic ordering remain distinct in every output format
