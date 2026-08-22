## ADDED Requirements

### Requirement: Gate composes complete persistent-debt and optional weakening evidence
The system SHALL provide one typed architecture-debt gate that collects complete authoritative current architecture candidates through the existing baseline lifecycle, compares them with an explicit reviewed baseline, and optionally consumes a pair of explicit base/current policy-context artifacts through the existing policy-weakening comparer. It SHALL preserve evaluation, persistent debt, and policy weakening as separate sections and SHALL not introduce a third validation mode.

#### Scenario: Known strict debt remains visible without becoming new debt
- **WHEN** a strict architecture finding exactly matches a reviewed baseline identity
- **THEN** the gate reports it as matched persistent debt and does not fail solely for that finding

#### Scenario: Audit finding is classified without changing mode semantics
- **WHEN** an audit finding is not present in the reviewed baseline
- **THEN** the gate reports it as new persistent debt without treating audit as strict validation mode

### Requirement: Persistent-debt gate remains exact and fail-closed
The persistent-debt section SHALL reuse the existing versioned canonical identity and lifecycle vocabulary for new, matched, resolved, stale/configuration, and ambiguous entries. The gate SHALL fail when the comparison is untrusted or out of sync and SHALL never write or approve a baseline.

#### Scenario: A distinct occurrence remains new
- **WHEN** one occurrence inside a type/member is matched by the baseline and a second canonically distinct occurrence exists
- **THEN** the second occurrence is reported as new and causes persistent-debt gate failure

#### Scenario: Malformed or ambiguous baseline cannot pass
- **WHEN** the baseline comparison encounters malformed, stale, incompatible, or ambiguous state
- **THEN** the gate returns a non-success decision with deterministic explanation rather than reporting zero new debt

### Requirement: Policy weakening remains an independent gate input
When both policy-context artifacts are supplied, the gate SHALL consume the existing normalized policy-weakening result unchanged in semantic meaning. Error-severity findings SHALL fail the overall gate even with no new persistent debt; warning, reviewed migration, and `impact_not_proven` findings SHALL remain visible only in the policy-weakening section and SHALL not acquire baseline lifecycle fields. Supplying only one context artifact SHALL fail as invalid input.

#### Scenario: Error weakening fails a clean debt comparison
- **WHEN** persistent debt has zero new entries and policy weakening has an error-severity semantic finding
- **THEN** the overall gate fails while the persistent-debt section remains clean

#### Scenario: Warning weakening does not create debt
- **WHEN** policy weakening has warning-severity or impact-not-proven evidence
- **THEN** the gate reports the guardrail evidence without adding a baseline entry or failing solely because of that warning

### Requirement: Gate projections are normalized and deterministic
The gate SHALL expose one normalized result model in Human, JSON, SARIF, and Testing projections. Machine-readable output SHALL include canonical persistent-debt identity/status and independent weakening identity/classification/severity/evidence, with stable ordering.

#### Scenario: Formats preserve separate sections
- **WHEN** a gate result contains matched debt and a weakening finding
- **THEN** JSON, SARIF, and Testing expose both records with their respective typed semantics and neither is relabeled as the other
