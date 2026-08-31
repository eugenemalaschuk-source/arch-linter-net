## MODIFIED Requirements

### Requirement: Policies declare explicit absolute metric budgets
The policy SHALL support `strict_metric_budgets` and `audit_metric_budgets`
contract collections. Every budget SHALL have a unique non-empty contract ID
and reference exactly one declared metric ID. A budget without `baseline_mode`
SHALL declare at least one absolute integer `minimum` or `maximum` limit;
limits SHALL be non-negative and a budget declaring both SHALL require
`minimum` less than or equal to `maximum`.

A budget with `baseline_mode` SHALL use exactly one supported mode:
`no_worse_than_baseline` or `max_delta`. `no_worse_than_baseline` SHALL allow
no increase over the reviewed value. `max_delta` SHALL require a non-negative
integer `max_delta`. A relative budget MAY declare `maximum` as an independent
absolute safety cap, but SHALL NOT declare `minimum`. A budget SHALL NOT
declare a formula, script, independent subject selector, or metric kind not
already represented by its referenced metric definition. Invalid references or
incoherent combinations SHALL fail the ordinary schema or typed
policy-configuration path before architecture analysis.

#### Scenario: A strict no-worse budget references a declared metric
- **WHEN** a policy declares a `strict_metric_budgets` entry with a unique ID,
  a declared metric ID, and `baseline_mode: no_worse_than_baseline`
- **THEN** the budget is accepted as a strict contract that requires a matching
  reviewed metric baseline for that metric's native subject and scope

#### Scenario: A strict upper budget references a declared metric
- **WHEN** a policy declares a `strict_metric_budgets` entry with a unique ID,
  a declared metric ID, and `maximum: 3`
- **THEN** the budget is accepted as a strict architecture contract over that
  metric's native subject and scope

#### Scenario: A bounded delta budget has an absolute cap
- **WHEN** a budget declares `baseline_mode: max_delta`, `max_delta: 2`, and
  `maximum: 10`
- **THEN** it is accepted as a relative budget whose allowable threshold is
  bounded by both the reviewed value plus two and the absolute cap ten

#### Scenario: An incoherent limit is rejected as configuration
- **WHEN** a budget has no absolute or baseline-relative limit, references an
  undeclared metric, uses a negative limit or delta, sets `minimum` greater
  than `maximum`, supplies `max_delta` without `max_delta` mode, or combines a
  relative mode with `minimum`
- **THEN** the policy is rejected through the normal invalid-configuration path
  rather than producing an unassessable result or a threshold finding

### Requirement: Evaluable budgets enforce their configured bounds
An evaluable absolute budget SHALL produce one deterministic normal architecture
finding when its measured value is below `minimum` or above `maximum`; a value
equal to either boundary SHALL pass. A relative budget with a matching reviewed
metric baseline SHALL calculate its delta as current value minus baseline value.
Its allowed delta SHALL be zero for `no_worse_than_baseline` and the configured
`max_delta` for `max_delta`; its effective threshold SHALL be the lower of the
baseline-plus-allowed-delta threshold and an optional absolute maximum cap.
It SHALL produce one normal finding only when the current value exceeds that
effective threshold. Equality SHALL pass.

Every threshold finding SHALL identify the budget contract, metric identity and
native subject, measured value, breached bound and configured absolute limit
when one applies, effective scope, canonical contributors, and for a relative
budget its mode, baseline value, computed delta, allowed delta, effective
threshold, and optional absolute cap. A passing value SHALL not produce a
budget finding; it remains available through the measure workflow.

#### Scenario: A relative delta is exceeded
- **WHEN** a relative budget has reviewed value four, `max_delta: 2`, and an
  evaluable current value seven
- **THEN** it produces one finding with current value seven, baseline four,
  delta three, allowed delta two, threshold six, and sorted contributors

#### Scenario: An absolute cap limits legacy debt
- **WHEN** a no-worse budget has reviewed value twelve, current value eleven,
  and `maximum: 10`
- **THEN** it produces one finding for the absolute cap rather than allowing
  the current value solely because it is lower than the baseline

#### Scenario: A relative boundary value passes
- **WHEN** a budget has reviewed value three, `max_delta: 2`, and current value
  five with no lower absolute cap
- **THEN** normal validation produces no budget finding

#### Scenario: An upper limit is exceeded
- **WHEN** an evaluable outgoing-component metric for topology node `application`
  has value four with `maximum: 3`
- **THEN** its budget produces one finding that identifies value four, maximum
  three, node `application`, and the sorted contributors that made up the value

#### Scenario: A boundary value passes
- **WHEN** an evaluable absolute metric has value three and its budget declares
  `maximum: 3`
- **THEN** normal validation produces no budget finding

### Requirement: Budget findings use the canonical result envelope
Budget threshold findings SHALL use the normal canonical finding identity and
finding-level baseline matching flow, and SHALL be represented in the existing
Human, JSON, SARIF, and Testing outputs without a budget-specific result
envelope. Metric baseline identity and value SHALL remain a separate input to
relative evaluation and SHALL NOT become a finding-level baseline identity.
Strict budget findings SHALL participate in strict validation failure and audit
budget findings SHALL remain audit findings under established mode semantics.

#### Scenario: A relative budget finding is surfaced in every output
- **WHEN** a strict baseline-relative metric budget is exceeded during
  validation
- **THEN** strict validation, JSON, SARIF, and the Testing adapter expose the
  same current value, reviewed baseline, delta, allowed delta, threshold,
  optional cap, subject identity, and contributor evidence

#### Scenario: A budget finding is surfaced in strict and machine-readable output
- **WHEN** a strict absolute metric budget is exceeded during validation
- **THEN** the same canonical finding is available to strict validation,
  baseline comparison, JSON, SARIF, and the Testing adapter with its measured
  value, configured limit, subject identity, and contributor evidence

### Requirement: Budget governance facts remain available to static policy consumers
The effective policy context SHALL project every metric-budget contract with its
declared metric identity, configured absolute `minimum` and `maximum` bounds,
and any baseline mode or maximum delta. Static policy consumers SHALL use those
typed facts without triggering metric evaluation, baseline loading, or
architecture analysis.

#### Scenario: A context exports a relative metric budget
- **WHEN** a policy declares a strict metric budget with `metric: components`,
  `baseline_mode: max_delta`, and `max_delta: 1`
- **THEN** its effective policy context contains typed `metric`,
  `baseline_mode`, and `max_delta` facts without reporting an unsupported
  contract type

#### Scenario: A context exports a one-sided metric budget
- **WHEN** a policy declares a strict metric budget with `metric: components`
  and `maximum: 10`
- **THEN** its effective policy context contains typed `metric` and `maximum`
  facts for that budget and does not report an unsupported contract type
