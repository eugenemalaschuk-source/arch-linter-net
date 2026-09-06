## ADDED Requirements

### Requirement: Test adapter consumes CLI policy-context exports

`ArchitectureValidationBuilder.WithPolicyWeakeningContexts(baseContextPath, currentContextPath).EvaluateDebtGate(...)` SHALL accept the current versioned `architecture-policy-context` JSON artifacts emitted by the CLI's `policy context --format json` command when both artifacts are valid and comparable. The Testing path SHALL use the same Core-owned import and validation contract as the CLI, while continuing to fail closed for malformed, unsupported, incomplete, or incompatible artifacts.

#### Scenario: Schema-5 CLI contexts reach the Testing debt gate

- **WHEN** base and current policy-context files contain the current `schema_version`, the `architecture-policy-context` kind, complete effective-policy evidence, and the same policy identity
- **THEN** `EvaluateDebtGate(...)` SHALL return an `ArchitectureDebtGateOutcome` instead of throwing an unsupported-schema or unsupported-kind exception

#### Scenario: Strict-to-audit weakening remains blocking through Testing

- **WHEN** the current CLI-exported context changes a matching strict contract to audit and the persistent-debt comparison is otherwise clean
- **THEN** the returned outcome SHALL expose policy-weakening evidence with error severity and SHALL set `Passed` to `false`

#### Scenario: Identical CLI contexts remain a no-op

- **WHEN** the same valid CLI-exported context is supplied as both base and current input
- **THEN** the returned outcome SHALL contain no policy-weakening errors and SHALL preserve the ordinary debt-gate result

#### Scenario: Invalid context input still fails closed

- **WHEN** either supplied context is malformed, incomplete, unsupported, or incompatible with the other context
- **THEN** the Testing path SHALL reject the input deterministically rather than treating it as an empty or passing comparison
