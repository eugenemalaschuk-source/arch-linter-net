## Purpose

Define trusted governance assessment completion so required evidence that cannot
support a decision is never rendered as a clean, empty architecture result.

## ADDED Requirements

### Requirement: Assessment completion distinguishes trust from conformance
An authoritative governance assessment SHALL expose one typed completion state:
`pass`, `fail`, or `unassessable`. `pass` means all requested authoritative
controls were evaluable and the configured gate passed. `fail` means the
assessment was trusted and at least one configured authoritative architecture
control failed. `unassessable` means the policy/configuration was valid but
one or more requested authoritative controls lacked sufficient required
architecture evidence for a trustworthy decision.

Invalid CLI invocation, policy syntax/schema/configuration errors, and runtime
or output-routing failures SHALL remain on their existing invalid-input or
runtime failure paths; they SHALL NOT be represented as assessment
`unassessable` or ordinary architecture violations.

#### Scenario: Required evidence is insufficient
- **WHEN** a valid requested authoritative control has missing, stale,
  wrong-context, unexpectedly empty, unmapped, ambiguous, malformed, or
  otherwise insufficient required evidence
- **THEN** the completed assessment state is `unassessable` and is distinct
  from both a trusted `fail` and an invalid invocation or policy

#### Scenario: Trusted architecture violation remains a failure
- **WHEN** every requested authoritative control is evaluable and a configured
  strict architecture contract has a blocking violation
- **THEN** the completed assessment state is `fail`, not `unassessable`

### Requirement: V0.8 evidence requirements are explicit and opt-in
Every v0.8 governance family that relies on applicability evidence SHALL make
each input's required or optional-empty semantics explicit in its validated
policy schema and canonical effective-control membership. A missing required
topology input, external diagnostic artifact, subject universe, mapping fact,
or declared selector result SHALL create unassessable evidence rather than an
implicit empty collection or zero findings. An optional absence SHALL be
permitted only when the effective policy explicitly declares it optional and
shall remain distinct from a supplied but invalid optional input.

Existing policies and families that do not opt into v0.8 applicability
semantics SHALL preserve their current behavior.

#### Scenario: Required selector matches no subjects
- **WHEN** an effective required v0.8 control's validated selector resolves
  to zero subjects without an explicit optional-empty policy declaration
- **THEN** the control supplies deterministic `unexpected_empty_input`
  unassessable evidence rather than a clean zero-result evaluation

#### Scenario: Optional external artifact is absent
- **WHEN** an effective external-diagnostics control explicitly declares its
  artifact optional and no artifact is supplied
- **THEN** the control is `not_applicable` with optional-policy provenance and
  does not make the assessment unassessable

### Requirement: Unassessability has canonical reasons and provenance
Every unassessable completion SHALL retain deterministic reason codes and
canonical control/family/policy provenance. Reason codes SHALL distinguish at
least missing required input, unexpected empty input, unmapped subject,
ambiguous subject, stale declaration, malformed external input, wrong external
repository, revision, or scope, and missing or invalid applicability-record
integrity. Display text, local paths, timestamps, and finding counts SHALL NOT
be the machine-readable reason or identity.

#### Scenario: Multiple insufficient inputs remain explainable
- **WHEN** one required control has a stale declaration and another is missing
  a required external artifact
- **THEN** both deterministic reason/provenance records remain available in
  stable order and neither is flattened into a generic zero-findings result

### Requirement: Assessment completion fails closed across requested controls
For an authoritative assessment, any requested required control with an
unassessable applicability or completeness outcome SHALL make the overall
completion state `unassessable`. Optional/not-applicable controls SHALL remain
visible but SHALL not become required denominators or cause unassessability
solely because they are deliberately absent. Completion aggregation SHALL not
infer evaluability from a zero-finding count, a configured-control count, or a
missing record.

#### Scenario: Missing applicability record cannot become a pass
- **WHEN** the expected membership contains a required control whose
  applicability record is missing
- **THEN** the control stays in scope with `missing_applicability_record`
  evidence and the authoritative assessment is `unassessable`
