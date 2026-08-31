# architecture-metric-budgets Specification

## Purpose
Define deterministic strict and audit absolute budget contracts over the
existing architecture metric catalog without duplicating measurement semantics.

## Requirements

### Requirement: Policies declare explicit absolute metric budgets
The policy SHALL support `strict_metric_budgets` and `audit_metric_budgets`
contract collections. Every budget SHALL have a unique non-empty contract ID,
reference exactly one declared metric ID, and declare at least one absolute
integer `minimum` or `maximum` limit. Limits SHALL be non-negative, and a
budget declaring both limits SHALL require `minimum` less than or equal to
`maximum`. A budget SHALL NOT declare a formula, script, baseline-relative
limit, independent subject selector, or metric kind not already represented by
its referenced metric definition. Invalid references or limits SHALL fail the
ordinary schema or typed policy-configuration path before architecture analysis.

#### Scenario: A strict upper budget references a declared metric
- **WHEN** a policy declares a `strict_metric_budgets` entry with a unique ID,
  a declared metric ID, and `maximum: 3`
- **THEN** the budget is accepted as a strict architecture contract over that
  metric's native subject and scope

#### Scenario: An incoherent limit is rejected as configuration
- **WHEN** a budget has no limit, references an undeclared metric, uses a
  negative limit, or sets `minimum` greater than `maximum`
- **THEN** the policy is rejected through the normal invalid-configuration path
  rather than producing an unassessable result or a threshold finding

### Requirement: Budgets reuse the metric measurement authority
Each budget SHALL use the same deterministic metric evaluator, native target,
counting universe, contributor set, contributor ordering, and effective-scope
semantics as `measure` for its referenced metric. It SHALL not re-scan source
or assemblies, create a second graph, derive transitive relations, replace a
metric contributor set, or compute a separate budget-specific number. When
several budgets reference one metric in an assessment, they SHALL compare the
same measured value and contributor evidence.

#### Scenario: Multiple budgets compare one measured value
- **WHEN** strict and audit budgets reference the same complete metric whose
  canonical contributor set has value four
- **THEN** each applicable budget compares value four with its own absolute
  limits and reports the same contributor evidence without a separate metric
  calculation

### Requirement: Evaluable budgets enforce their configured bounds
An evaluable budget SHALL produce one deterministic normal architecture finding
when its measured value is below `minimum` or above `maximum`; a value equal to
either boundary SHALL pass. The finding SHALL identify the budget contract,
metric identity and native subject, measured value, breached bound and configured
limit, effective scope, and canonical contributors. A passing value SHALL not
produce a budget finding; it remains available through the measure workflow.

#### Scenario: An upper limit is exceeded
- **WHEN** an evaluable outgoing-component metric for topology node `application`
  has value four with `maximum: 3`
- **THEN** its budget produces one finding that identifies value four, maximum
  three, node `application`, and the sorted contributors that made up the value

#### Scenario: A boundary value passes
- **WHEN** an evaluable metric has value three and its budget declares
  `maximum: 3`
- **THEN** normal validation produces no budget finding

### Requirement: Insufficient metric scope is projected through common applicability evidence
The assessment SHALL reuse the normalized applicability and completion projection
when a budget's referenced metric has missing required input or an unmapped,
ambiguous, stale, unexpectedly empty, unresolved, or otherwise incomplete native
scope. It SHALL retain deterministic policy/control identity,
reason, provenance, and strict/audit mode through the existing Human, JSON,
SARIF, Testing, and baseline result paths. It SHALL not publish a partial
numeric value, silently pass the budget, or recast insufficient evidence as an
ordinary threshold violation.

#### Scenario: An unmapped endpoint cannot lower a maximum budget
- **WHEN** a budgeted component metric has one mapped contributor and one
  required dependency endpoint that cannot map uniquely to a topology node
- **THEN** the assessment emits common unassessable applicability evidence for
  that budget scope and does not compare a partial value of one with its limit

### Requirement: Budget findings use the canonical result envelope
Budget threshold findings SHALL use the normal canonical finding identity and
baseline matching flow, and SHALL be represented in the existing Human, JSON,
SARIF, and Testing outputs without a budget-specific result envelope. Strict
budget findings SHALL participate in strict validation failure and audit budget
findings SHALL remain audit findings under the established mode semantics.

#### Scenario: A budget finding is surfaced in strict and machine-readable output
- **WHEN** a strict metric budget is exceeded during validation
- **THEN** the same canonical finding is available to strict validation,
  baseline comparison, JSON, SARIF, and the Testing adapter with its measured
  value, configured limit, subject identity, and contributor evidence

### Requirement: Budget governance facts remain available to static policy consumers
The effective policy context SHALL project every metric-budget contract with its
declared metric identity and each configured absolute `minimum` and `maximum`
bound. Static policy consumers SHALL use those typed facts without triggering
metric evaluation or architecture analysis.

#### Scenario: A context exports a one-sided metric budget
- **WHEN** a policy declares a strict metric budget with `metric: components`
  and `maximum: 10`
- **THEN** its effective policy context contains typed `metric` and `maximum`
  facts for that budget and does not report an unsupported contract type
