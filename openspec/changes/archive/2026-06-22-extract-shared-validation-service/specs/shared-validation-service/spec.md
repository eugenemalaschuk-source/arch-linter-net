## ADDED Requirements

### Requirement: One service drives CLI validation, the public API, and the Testing adapter
`ArchLinterNet.Core.Validation.ArchitectureValidationService.Validate` SHALL be the single implementation of the policy-load → condition-set-resolution → repository-root-resolution → assembly-resolution → runner-creation → contract-execution → result-aggregation pipeline. The CLI `validate` command, the public `ArchitectureValidator` API, and the `ArchitectureAssertions` Testing adapter SHALL all call this service rather than re-implementing the pipeline.

#### Scenario: CLI validate delegates to the service
- **WHEN** the CLI `validate` command runs
- **THEN** it SHALL build a `ValidationRequest` and call `ArchitectureValidationService.Validate`, performing no policy loading, condition-set resolution, assembly resolution, or contract execution itself

#### Scenario: Public API delegates to the service
- **WHEN** `ArchitectureValidator.Validate(...)` is called
- **THEN** it SHALL build a `ValidationRequest` with `Mode = "strict"` and call `ArchitectureValidationService.Validate`, translating the returned `ValidationOutcome` into its `out` parameters and boolean return value

#### Scenario: Testing adapter delegates to the service
- **WHEN** `ArchitectureValidationBuilder.ValidateStrict()` or `ValidateAudit()` is called
- **THEN** it SHALL build a `ValidationRequest` with the corresponding `Mode` and call `ArchitectureValidationService.Validate`, wrapping the returned `ValidationOutcome` in an `ArchitectureValidationResult`

### Requirement: ValidationRequest models a validation run
`ValidationRequest` SHALL carry: `PolicyPath` (required), `Mode` (required, `"strict"` or `"audit"`), `ConditionSetName` (optional), `PreprocessorSymbols` (optional explicit override that bypasses condition-set resolution when set), `ContractIds` (optional contract-ID filter), `BaselinePath` (optional baseline file to merge), `IncludeAsmdefContracts` (default `true`), and `EnforceUnmatchedIgnoredViolationsPolicy` (default `false`).

#### Scenario: Invalid mode rejected
- **WHEN** `ArchitectureValidationService.Validate` is called with a `ValidationRequest.Mode` that is neither `"strict"` nor `"audit"`
- **THEN** it SHALL throw `ArgumentException`

#### Scenario: Explicit preprocessor symbols bypass condition-set resolution
- **WHEN** `ValidationRequest.PreprocessorSymbols` is non-null
- **THEN** the service SHALL use those symbols directly and SHALL NOT invoke condition-set resolution

### Requirement: ValidationOutcome models a validation result
`ValidationOutcome` SHALL carry: `Passed`, `Violations`, `Cycles`, `UnmatchedIgnoredViolations`, and `UnmatchedIgnoredViolationsConfig`.

#### Scenario: Passed reflects violations and cycles
- **WHEN** a validation run produces no violations and no cycles
- **THEN** `ValidationOutcome.Passed` SHALL be `true`

#### Scenario: Passed reflects blocking unmatched-ignored-violations only when enforced
- **WHEN** `ValidationRequest.EnforceUnmatchedIgnoredViolationsPolicy` is `false` and the policy has unmatched ignored violations under an `"error"` configuration
- **THEN** `ValidationOutcome.Passed` SHALL NOT be affected by those unmatched ignored violations

### Requirement: IncludeAsmdefContracts controls asmdef contract execution
When `ValidationRequest.IncludeAsmdefContracts` is `true`, the service SHALL execute `strict_asmdef`/`audit_asmdef` contracts for the active mode. When `false`, it SHALL skip them entirely.

#### Scenario: Public API runs asmdef contracts
- **WHEN** `ArchitectureValidator.Validate(...)` validates a policy with a violated `strict_asmdef` contract
- **THEN** the result SHALL be `false` and the violation SHALL be present in the returned violations

#### Scenario: Testing adapter runs asmdef contracts
- **WHEN** `ArchitectureValidationBuilder.ValidateStrict()` validates a policy with a violated `strict_asmdef` contract
- **THEN** `ArchitectureValidationResult.Passed` SHALL be `false`

### Requirement: EnforceUnmatchedIgnoredViolationsPolicy controls unmatched-ignored-violations gating
When `ValidationRequest.EnforceUnmatchedIgnoredViolationsPolicy` is `true`, the service SHALL validate the policy's `analysis.unmatched_ignored_violations` value (one of `"error"`, `"warn"`, `"off"`), throwing `InvalidOperationException` if invalid, and SHALL fail the run (`Passed = false`) when that value is `"error"` and unmatched ignored violations exist. When `false`, the service SHALL NOT validate that field and SHALL NOT let unmatched ignored violations affect `Passed`.

#### Scenario: CLI validate enforces the policy
- **WHEN** the CLI `validate` command runs against a policy with `analysis.unmatched_ignored_violations: error` and unmatched ignored violations exist
- **THEN** the CLI SHALL exit with code 1

#### Scenario: Public API does not enforce the policy
- **WHEN** `ArchitectureValidator.Validate(...)` runs against a policy with `analysis.unmatched_ignored_violations: error` and unmatched ignored violations exist, but no other violations or cycles
- **THEN** the result SHALL be `true`

### Requirement: Shared setup and execution building blocks
`ArchLinterNet.Core.Execution.ArchitectureRunnerFactory` SHALL provide `LoadDocument` (policy load + optional baseline merge) and `BuildRunner` (repository-root resolution, condition-set resolution, assembly resolution, runner construction). `ArchLinterNet.Core.Execution.ArchitectureContractExecutor` SHALL provide `Execute`, which runs all contract families for a given mode (`strict`/`audit`) against a runner and returns aggregated violations and cycles, including layer-template expansion.

#### Scenario: Baseline generation reuses the shared building blocks
- **WHEN** the CLI `baseline generate` command runs
- **THEN** it SHALL call `ArchitectureRunnerFactory.LoadDocument`/`BuildRunner` and `ArchitectureContractExecutor.Execute` for setup and contract execution, rather than re-implementing them, while keeping baseline-specific control flow (the configuration-violation early exit, and running both `strict` and `audit` modes for `--mode all`) in the CLI layer

#### Scenario: Baseline generation does not run asmdef contracts
- **WHEN** `baseline generate` calls `ArchitectureContractExecutor.Execute`
- **THEN** it SHALL pass `includeAsmdefContracts: false`, so `strict_asmdef`/`audit_asmdef` contracts are never included in generated baselines
