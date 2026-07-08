## ADDED Requirements

### Requirement: Public API exposes a ValidationRequest-based overload
`ArchLinterNet.Core.ArchitectureValidator` SHALL expose `ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)`, which SHALL build no request of its own, call `ArchitectureEngine.Validate(request, timing)` directly, and return the resulting `ValidationOutcome` unmodified (no folding of coverage or policy-consistency findings into a flat violations collection). This overload SHALL support every field `ValidationRequest` carries, including `ContractIds`, `ConditionSetName`, `BaselinePath`, and `EnforceUnmatchedIgnoredViolationsPolicy`, giving programmatic callers the same functional choices as the CLI `validate` command.

#### Scenario: Caller selects contracts and enforces unmatched-ignored policy
- **WHEN** `ArchitectureValidator.Validate(request)` is called with `request.ContractIds` set to a subset of contract IDs and `request.EnforceUnmatchedIgnoredViolationsPolicy = true`
- **THEN** only the selected contracts SHALL run and `ValidationOutcome.Passed` SHALL reflect the policy's `analysis.unmatched_ignored_violations` configuration exactly as the CLI would

#### Scenario: Caller merges a baseline
- **WHEN** `ArchitectureValidator.Validate(request)` is called with `request.BaselinePath` set to a valid baseline file
- **THEN** the baseline's ignored entries SHALL be merged into the policy before contract execution, identically to CLI `validate --baseline`

#### Scenario: Existing overloads remain unaffected
- **WHEN** any of the three pre-existing `ArchitectureValidator.Validate(...)` overloads (positional `policyPath`, `out violations`, or `out violations`/`out cycles` with optional `preprocessorSymbols`) is called
- **THEN** behavior SHALL be identical to before this requirement was added: `Mode = "strict"` and `EnforceUnmatchedIgnoredViolationsPolicy = false` are still used internally, and violations continue to be folded (coverage findings and policy-consistency findings included) into the `out` collection as before
