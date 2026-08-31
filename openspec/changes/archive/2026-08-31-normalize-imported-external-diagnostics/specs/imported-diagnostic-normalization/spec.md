## ADDED Requirements

### Requirement: Selected external diagnostics project into governed findings
The system SHALL project only `SarifSelectedExternalDiagnostic` instances produced by the trusted
selection boundary into a typed imported-diagnostic detail and the existing normalized
`ArchitectureFinding` envelope. The projection SHALL preserve the selected policy governance mode
and SHALL NOT read an artifact, query a producer, or reapply a diagnostic filter.

#### Scenario: A selected strict diagnostic becomes a governed finding
- **WHEN** #521 supplies one selected diagnostic mapped to strict
- **THEN** the projection returns one imported normalized finding with strict/error semantics and
  no second trust or selection decision

#### Scenario: An untrusted artifact has no governed diagnostic
- **WHEN** #520 rejects an artifact before #521 selection
- **THEN** the imported-diagnostic projection receives no selected source diagnostic and creates
  no ordinary imported finding

### Requirement: Imported finding identity is stable and evidence provenance remains drillable
An imported finding's canonical persistent identity SHALL be deterministic from the selected
diagnostic's stable semantic identity, logical evidence control, and governance semantics. It
SHALL distinguish selected diagnostics that differ in required logical evidence, repository,
revision, scope, source location, source severity, or mapped mode where #521 distinguishes them.
It SHALL exclude source display text, artifact content hash, run identity, artifact path, and
producer run ordering. The finding detail SHALL retain original tool/rule/message/severity/location,
fingerprint origin/value, and every ordered evidence provenance entry including logical key,
tool/version/run, repository/revision/scope, artifact path, and content hash.

#### Scenario: Equivalent reruns retain debt identity and update provenance
- **WHEN** equivalent current-context source results are selected from two runs with different
  artifact hashes or run IDs
- **THEN** their projected finding identity remains stable while the ordered provenance keeps both
  authorizing run/hash entries

#### Scenario: Different source locations do not collide
- **WHEN** two selected source results have the same rule and source fingerprint but different
  normalized primary locations
- **THEN** the projection produces distinct canonical finding identities and source locations

### Requirement: Imported findings yield exact baseline candidates without a parallel lifecycle
The system SHALL expose baseline candidates for imported findings from the same structured identity
used by their normalized finding. It SHALL use existing baseline candidate/comparison structures
and SHALL NOT write, suppress, or reinterpret a baseline entry during projection.

#### Scenario: Exact known imported finding remains known
- **WHEN** an imported finding is projected again from equivalent current-context evidence
- **THEN** its baseline candidate has the same structured identity and can match the existing
  reviewed baseline entry without source-message, artifact-hash, or run-ID churn
