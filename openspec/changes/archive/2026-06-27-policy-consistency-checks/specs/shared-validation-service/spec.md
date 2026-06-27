## ADDED Requirements

### Requirement: Policy consistency check runs as part of the shared pipeline
`ArchitectureValidationService.Validate` SHALL run the policy-consistency check for every `ValidationRequest`, regardless of `Mode`, as a fixed pipeline step between configuration validation and contract execution, so the CLI `validate` command, the public `ArchitectureValidator` API, and the Testing adapter all pick it up automatically.

#### Scenario: CLI validate surfaces policy consistency findings
- **WHEN** the CLI `validate` command runs against a policy with a duplicate contract ID and `analysis.policy_consistency` is unset (defaulting to `error`)
- **THEN** the CLI SHALL exit with code 1 and report the duplicate-ID finding

#### Scenario: Public API surfaces policy consistency findings
- **WHEN** `ArchitectureValidator.Validate(...)` runs against a policy with an allow/forbid conflict and `analysis.policy_consistency: error`
- **THEN** the result SHALL be `false` and the conflict SHALL be present in the returned violations

#### Scenario: Testing adapter surfaces policy consistency findings
- **WHEN** `ArchitectureValidationBuilder.ValidateStrict()` runs against a policy with an independence conflict and `analysis.policy_consistency: error`
- **THEN** `ArchitectureValidationResult.Passed` SHALL be `false`

### Requirement: ValidationOutcome carries policy consistency findings
`ValidationOutcome` SHALL carry `PolicyConsistencyFindings` (a collection of `PolicyConsistencyDiagnostic`) separate from `Violations` and `Cycles`, and SHALL fold them into `Passed` according to the resolved `analysis.policy_consistency` severity.

#### Scenario: Findings present but not failing under warn
- **WHEN** `analysis.policy_consistency: warn` and the policy has a protected-importer conflict
- **THEN** `ValidationOutcome.PolicyConsistencyFindings` SHALL contain the conflict and `ValidationOutcome.Passed` SHALL NOT be affected by it

#### Scenario: Invalid policy_consistency value rejected
- **WHEN** `ArchitectureValidationService.Validate` is called against a policy with `analysis.policy_consistency` set to a value other than `error`, `warn`, or `off`
- **THEN** it SHALL throw `InvalidOperationException`
