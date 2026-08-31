## MODIFIED Requirements

### Requirement: Single-mode validation remains simple and unchanged
The system SHALL implement
`IArchitectureValidationApplicationService.Validate(ValidationRequest, ValidationTiming?)`
on top of `CreateSnapshot` and `Evaluate`, with the snapshot disposed before
`Validate` returns. The returned `ValidationOutcome` SHALL preserve the
requested mode's findings, `Waivers`, waiver gating, pass/fail result, and all
other mode-local semantics. Its policy inventory MAY additionally include
repository-level lifecycle evidence from selected companion modes, without
imposing a new object or disposal responsibility on the caller.

#### Scenario: Existing single-mode callers are unaffected
- **WHEN** an existing caller invokes `Validate` for a single mode
- **THEN** the returned outcome retains the requested mode's findings,
  `Waivers`, gating, and pass/fail result, while its policy inventory can carry
  the canonical repository-wide waiver evidence

## ADDED Requirements

### Requirement: Repository inventory completes selected waiver lifecycle evidence
Before returning a non-blocked `ValidationOutcome` with a policy inventory, an
`ArchitectureAnalysisSnapshot` SHALL obtain normal canonical waiver lifecycle
records for every selected strict or audit mode that declares a manual waiver.
It SHALL combine those records into the Core-owned repository policy inventory.
The outcome's `Waivers`, waiver gating, findings, and pass/fail value SHALL
remain the results of its requested mode only. Companion lifecycle work SHALL
use normal snapshot cache lookup and per-mode memoization, and a later explicit
evaluation of that companion mode SHALL reuse the completed outcome.

#### Scenario: Strict outcome carries audit waiver debt without audit gating
- **WHEN** a strict validation requests a policy with selected strict and audit
  waivers
- **THEN** the strict outcome retains only its strict `Waivers` and strict
  gating result, while its policy inventory includes both strict and audit
  waiver lifecycle records

#### Scenario: Companion outcome shares the canonical inventory
- **WHEN** an audit mode is completed as companion lifecycle work for a strict
  outcome and the caller later evaluates audit explicitly
- **THEN** audit returns its memoized mode-local outcome with the same
  repository policy inventory as strict, without another audit execution
