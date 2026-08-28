# governance-applicability-evidence Specification

## Purpose
Define deterministic control-level applicability evidence for v0.8 governance
families without conflating proven evaluability with policy configuration or
architecture quality.

## Requirements

### Requirement: Effective controls have explicit applicability membership
For every effective v0.8 control whose result depends on applicability or
completeness evidence, the system SHALL emit exactly one deterministic
control-level applicability record. The record SHALL reference the canonical
effective-control identity supplied by the effective-policy authority, rather
than a display name, YAML position, runtime expansion, or finding identity.

The record SHALL declare whether applicability proof is `required`, `optional`,
or `not_applicable` under explicit family/policy semantics. A control that is
not a member of the applicability-required set SHALL remain explicitly visible
as optional or not applicable; it SHALL NOT be silently omitted.

#### Scenario: Required control is counted exactly once
- **WHEN** one effective control is expanded across multiple source sets or
  yields multiple findings
- **THEN** its applicability record retains one canonical control identity and
  contributes exactly one member to the applicability-required set when its
  membership is `required`

#### Scenario: Optional control is visible without inflating the denominator
- **WHEN** explicit policy semantics make a configured control's input optional
- **THEN** the record identifies it as `optional` or `not_applicable`, is
  disclosed separately from required members, and does not silently count as an
  evaluable required control

### Requirement: Applicability state is typed and non-inferential
Each control-level applicability record SHALL expose exactly one typed state:
`evaluable`, `not_applicable`, or `unassessable`. A `required` record SHALL be
`evaluable` only when all evidence required by that control's declared
applicability semantics is sufficient and complete; otherwise it SHALL be
`unassessable`. `not_applicable` SHALL be used only when explicit policy or
family semantics make evaluation unnecessary or optional; it SHALL NOT mask
missing required evidence.

Zero findings, a zero measured value, an empty result collection, a missing
record, or a configured control count SHALL NOT be inferred to mean
`evaluable`.

#### Scenario: Empty successful result remains evaluable only with proof
- **WHEN** a required control produces zero findings or a neutral measurement
- **THEN** it is `evaluable` only if its record proves the required input and
  family evidence were complete; otherwise it is `unassessable`

#### Scenario: Missing record cannot improve a summary
- **WHEN** a downstream projection receives an effective control that requires
  applicability proof but has no control-level applicability record
- **THEN** the projection treats that control as unassessable and does not
  remove it from the required denominator or infer a complete result

### Requirement: Unassessable state preserves stable reason and provenance
An `unassessable` record SHALL contain one or more deterministic reason classes
and canonical provenance sufficient to identify the insufficient evidence.
Supported reason classes SHALL distinguish, where meaningful to the family,
missing or unavailable required input, unexpected empty input, unmapped
subject, ambiguous subject, stale declaration, malformed or failed external
input, and wrong external evidence identity, repository, revision, or scope.
Families MAY define additional bounded reason classes but SHALL NOT use display
text as the machine-readable reason.

#### Scenario: Stale and missing evidence stay distinguishable
- **WHEN** one control has a declared selector with no current match and another
  lacks its required input altogether
- **THEN** their records expose distinct stable reason classes and provenance
  rather than a shared zero-result status

### Requirement: Family evidence retains its native units
Applicability records SHALL retain family-specific evidence as drill-down
authority. For every evidence category meaningful to the family, evidence SHALL
identify its unit and contain deterministic counts with canonical
item/provenance references for subjects in scope, matched subjects, unmapped
subjects, ambiguous subjects, stale declarations, and external input or run
presence. A family SHALL record an evidence category as not applicable only
when that category has no semantic meaning for the configured control.

The model SHALL NOT combine types, topology nodes, dependency edges, files,
projects, assemblies, metrics, controls, or external runs into a generic score
or cross-family percentage. Counts SHALL be derived from the record's canonical
evidence collection and preserve the unit they count.

#### Scenario: Cross-family evidence is not arithmetically merged
- **WHEN** topology has unmapped observed subjects and external diagnostics has
  a missing required SARIF run
- **THEN** the resulting evidence retains the topology-subject and
  external-run units separately and does not add them into one completeness
  percentage or count

### Requirement: Applicability records have deterministic ordering and provenance
The canonical applicability collection SHALL order records by canonical
effective-control identity. Each record's evidence categories and item
references SHALL use their stable semantic identities and deterministic order.
Canonical equality SHALL not depend on display text, runtime enumeration order,
local timestamps, absolute paths, or random identifiers. Provenance SHALL be
sufficient to drill into the canonical control and its family evidence without
becoming a second control or finding identity.

#### Scenario: Equivalent analysis repeats identically
- **WHEN** the same effective policy and family evidence are evaluated twice
- **THEN** the control records, native evidence ordering, and provenance
  references are canonically equivalent across the two results

### Requirement: Family applicability matrices are explicit
The shared design SHALL define the following v0.8 applicability matrices for
the families that opt in:

| Family | Required evidence | Evaluable condition | Unassessable examples |
| --- | --- | --- | --- |
| Declared topology (#91) | declared topology control, observed subject universe, mapping and declaration evidence | every required observed subject is deterministically mapped or explicitly reviewed out of scope and declarations are current | missing subject universe, unmapped or ambiguous required subject, stale declaration |
| Contract-surface exposure (#92) | selected contract surface, visible signature/metadata facts, source and target classification evidence | the selected surface and required facts resolve completely for the configured control | missing selected surface, unresolved required fact, unexpected empty selector, stale declaration |
| Metrics and budgets (#93) | metric definition, target subject universe, and the measurement facts required by that metric | the metric's native counting universe and contributors are complete enough to trust its value | incomplete or unmapped target scope, ambiguous component, missing required measurement fact, unexpected empty input |
| External static diagnostics (#95) | logical evidence requirement, bounded SARIF artifact/run, and required trust binding | a successful matching run satisfies configured repository, revision, scope, producer, and logical-evidence binding | missing, malformed, failed, stale, wrong-key, wrong-repository, wrong-revision, or wrong-scope required input |

Each family SHALL make its own exact subject and declaration semantics explicit
when it implements a control; it SHALL use this matrix rather than inventing a
parallel result envelope.

#### Scenario: Valid current SARIF with no selected findings
- **WHEN** an external-diagnostics control receives a valid, successful,
  current-context SARIF run with zero selected diagnostics
- **THEN** its record is `evaluable` and distinguishes that state from a missing
  or stale required run

#### Scenario: Optional external evidence is not a failed required run
- **WHEN** an explicitly optional external-evidence control has no supplied
  artifact
- **THEN** its record is `not_applicable` with optional-policy provenance, not
  `unassessable` and not a successful zero-result run

### Requirement: Summaries derive from canonical membership and states
Downstream consumers SHALL derive applicability transparency summaries from the
explicit canonical control membership and records, not by counting findings,
YAML rules, rule categories, source-set expansions, or display strings. A
summary SHALL disclose the count of required controls, the count of evaluable
required controls, and any unassessable required controls. It SHALL keep
optional/not-applicable controls distinct.

The model SHALL permit deterministic summaries such as `38/38 evaluable` only
when all 38 explicit required members are evaluable. It SHALL permit effective
policy inventory to be displayed beside the summary while preserving the fact
that inventory/counting is owned by #685.

#### Scenario: Inventory and evaluability denominators differ
- **WHEN** the effective-policy inventory contains 42 controls and 38 have
  required applicability membership
- **THEN** the downstream consumer can display `42` effective controls and
  `38/38 evaluable` without recalculating either authority or presenting the
  two counts as interchangeable

### Requirement: Existing governance behavior remains unchanged until opt-in
The shared applicability contract SHALL not by itself add policy fields, alter
existing coverage classification, change strict/audit outcomes, create
normalized findings, change baseline identity, or affect existing policy
behavior. #506 owns fail-closed enforcement and #507 owns normalized
Human/JSON/SARIF/Testing projection; later family implementations opt in using
this contract.

#### Scenario: Existing policy has no v0.8 applicability control
- **WHEN** a policy contains only currently supported, pre-v0.8 contracts
- **THEN** it has identical loading, validation, finding, baseline, and output
behavior after this applicability contract is introduced
