# external-diagnostics-federation Specification

## Purpose
Define deterministic, vendor-neutral reference scenarios for the complete external-diagnostic
evidence federation path.
## Requirements
### Requirement: Synthetic reference scenarios preserve trusted external diagnostic semantics end to end
The system SHALL provide deterministic, public-safe synthetic SARIF reference scenarios that
compose bounded evidence trust validation, external-diagnostic selection, normalized finding and
baseline projection, output projection, and applicability evidence. A valid current-context
selected result SHALL retain its logical evidence key, producer/run, repository, revision, scope,
artifact content hash, source provenance, canonical identity, and strict/audit semantics through
the canonical finding, Human, JSON, SARIF, and Testing projections. The baseline projection SHALL
retain the selected diagnostic's stable canonical identity and strict/audit debt-lifecycle
semantics without retaining producer/run, artifact-path, or artifact-hash provenance. The
applicability projection SHALL report the external-evidence control's evaluable, unassessable, or
not-applicable state, associated reason codes, and general policy provenance without carrying the
selected diagnostic's provenance. A successful trusted zero-result artifact SHALL remain
explicitly evaluable and distinct from absent or unusable evidence.

#### Scenario: Current evidence produces a governed imported finding
- **WHEN** a synthetic SARIF 2.1.0 artifact has a successful matching producer run and every
  required current-context binding
- **THEN** the selected imported diagnostic appears in the canonical finding, Human, JSON, SARIF,
  and Testing projections with its evidence provenance intact
- **AND** the corresponding baseline candidate retains the selected canonical identity and
  strict/audit debt-lifecycle semantics
- **AND** the external-evidence applicability record is evaluable with no reason code

#### Scenario: Current evidence has no selected results
- **WHEN** a successful synthetic current-context artifact contains no policy-selected diagnostics
- **THEN** the external evidence applicability projection is evaluable and no imported finding is
  created

#### Scenario: Policy-authorized rule selection excludes nonmatching diagnostics
- **WHEN** a trusted synthetic artifact contains one diagnostic whose rule ID is authorized by the
  configured `rule_ids` filter and another diagnostic with the same source severity whose rule ID
  is not authorized
- **THEN** only the authorized diagnostic appears in selection, normalized findings, Human, JSON,
  SARIF, and Testing projections
- **AND** only the authorized diagnostic's stable canonical identity reaches baseline projection

#### Scenario: Native and imported diagnostics share canonical outputs
- **WHEN** a native architecture finding and a selected imported diagnostic are projected together
- **THEN** both remain deterministically identifiable without an identity collision or a separate
  external-diagnostic result envelope

#### Scenario: Compatibility evidence remains an external source finding
- **WHEN** a synthetic public-API compatibility diagnostic is supplied by a trusted external
  evidence producer
- **THEN** it is consumed as an imported finding with its external source and trust provenance,
  without invoking or recreating a compatibility-analysis engine

### Requirement: Synthetic reference scenarios fail closed and preserve deterministic occurrence identity
The system SHALL provide synthetic reference scenarios that prove required missing, malformed,
failed, incomplete, wrong logical-key, wrong repository, wrong revision, wrong scope, and
missing-required-binding evidence is unassessable rather than a clean result. The scenarios SHALL
also prove stable artifact hashing, order-independent repeated-result selection, source and
fallback fingerprint paths, distinct source locations, and distinct logical-evidence and scope
contexts without using analyzer execution, producer-service APIs, filenames, timestamps, or CI
job names as trust inputs.

#### Scenario: Required stale or invalid evidence cannot normalize
- **WHEN** a synthetic required evidence artifact is missing, unusable, stale, wrong-context, or
  missing a required binding
- **THEN** its applicability outcome is unassessable and it produces no selected or normalized
  imported finding

#### Scenario: Equivalent repeated evidence deduplicates deterministically
- **WHEN** equivalent current-context SARIF results occur in different artifact or result orders
- **THEN** selection yields stable canonical output and identity while retaining deterministic
  authorizing provenance and keeping distinct locations, logical-evidence controls, and scope
  contexts distinct

#### Scenario: Source location independently isolates canonical identity
- **WHEN** two valid source results have the same logical context, rule, severity, project,
  message, and source fingerprint but differ only by source path or region
- **THEN** selection and normalized finding projection retain two distinct canonical identities

#### Scenario: Reference protocol stays vendor-neutral
- **WHEN** the documented reference scenario describes external evidence consumption
- **THEN** it uses synthetic SARIF and explicit context bindings without naming, executing, or
  querying an analyzer or producer service
