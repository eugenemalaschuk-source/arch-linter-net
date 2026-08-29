## ADDED Requirements

### Requirement: ValidationOutcome carries assessment completion separately from findings
The shared validation pipeline SHALL expose typed assessment completion and its
ordered reason/provenance evidence on `ValidationOutcome`, independently of
ordinary violations, cycles, coverage, and configuration failures. A trusted
`pass` or `fail` SHALL retain existing conformance behavior. A valid-but-
unassessable authoritative assessment SHALL not be flattened into an ordinary
architecture violation merely to make `Passed` false. The Testing adapter SHALL
carry equivalent completion evidence for a result it maps from the shared
outcome.

#### Scenario: Testing observes an unassessable shared outcome
- **WHEN** the validation service returns an authoritative outcome with
  `unassessable` completion and a missing-required-input reason
- **THEN** the Testing adapter exposes the same completion state and reason
  without fabricating a violation or treating the outcome as a trusted pass

#### Scenario: Existing policy remains compatible
- **WHEN** a policy has no effective v0.8 applicability control
- **THEN** its shared validation outcome retains the existing pass/fail
  behavior and has no unassessable completion evidence
