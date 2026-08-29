## ADDED Requirements

### Requirement: Applicability evidence has one canonical normalized projection
The system SHALL derive a deterministic projection from the canonical expected
applicability-membership and produced-record join without reparsing policy YAML,
recounting effective controls, inferring membership from findings, or changing
family evidence. The projection SHALL retain each control's canonical
effective-control identity, family, membership, valid record state when one
exists, collection-integrity outcome, reason codes, and provenance.

The projection SHALL expose a summary with separate counts for
applicability-required controls, required evaluable controls, required
unassessable controls, optional controls, and not-applicable controls. The
required denominator SHALL contain every required expected control even when
its record is missing or invalid. An orphan, duplicate, or incompatible record
SHALL make the projection unassessable without manufacturing a reduced
denominator or a clean summary. Evaluability counts and ratios are completeness
transparency only; they SHALL NOT be represented as architecture quality
scores or combined with family-native units.

#### Scenario: Missing required evidence remains in the projection
- **WHEN** two expected required controls exist, one has a valid evaluable
  record, and the other's record is missing
- **THEN** the projection reports one of two required controls evaluable, one
  required control unassessable, and the missing-record reason/provenance for
  the second control

#### Scenario: Optional and not-applicable controls remain distinct
- **WHEN** a completed assessment contains an optional evaluable control and a
  not-applicable control
- **THEN** both controls remain identifiable in the projection, neither
  increases the required denominator, and their distinct membership/state
  semantics are preserved

### Requirement: Unassessable applicability evidence uses normalized outputs
Every valid unassessable applicability record and collection-integrity outcome
SHALL be projectable as an additive normalized finding using the canonical
effective-control identity and machine-readable reason/provenance. Human, JSON,
SARIF, and Testing outputs SHALL expose equivalent identity and typed evidence;
Human output MAY vary in prose but SHALL not discard the reason or provenance.
The strict/audit severity of an applicability finding SHALL follow the existing
mode projection and SHALL NOT reclassify a valid-but-unassessable assessment as
an ordinary architecture violation or an invalid policy/configuration error.

Where the existing baseline lifecycle accepts the finding's canonical identity,
applicability findings SHALL use that lifecycle without a second debt identity
algorithm or a display-text key. Evaluable and explicitly not-applicable states
SHALL remain control-evaluability evidence and SHALL NOT create artificial
failure findings.

#### Scenario: Generic projection is identical across machine consumers
- **WHEN** an applicability control is unassessable because its required input
  is unexpectedly empty
- **THEN** JSON, SARIF, and Testing expose the same canonical control identity,
  `unexpected_empty_input` reason, and provenance through the normalized
  finding projection

#### Scenario: Baseline identity does not depend on output prose
- **WHEN** an applicability finding is rendered in Human and SARIF formats
- **THEN** its baseline-capable canonical identity is identical in both formats
  and does not derive from either rendered message
