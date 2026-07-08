## 1. Core: ArchitectureValidator overload

- [x] 1.1 Add `ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)` to `src/ArchLinterNet.Core/ArchitectureValidator.cs`, delegating directly to `_engine.Value.Validate(request, timing)`
- [x] 1.2 Add tests in `tests/ArchLinterNet.Core.Tests/ArchitectureValidatorTests.cs` covering: contract-ID selection, condition-set selection, baseline merge, unmatched-ignored enforcement on/off, and that the three pre-existing overloads are unaffected

## 2. Testing: ArchitectureValidationBuilder fluent methods

- [x] 2.1 Add `WithContracts(IEnumerable<string> contractIds)` and `WithContracts(params string[] contractIds)` to `src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs`
- [x] 2.2 Add `WithBaseline(string baselinePath)`
- [x] 2.3 Add `WithUnmatchedIgnoredViolationsPolicy(bool enforce = true)`
- [x] 2.4 Add `WithTimings()`, allocating a `ValidationTiming` and threading it through to `_engine.Value.Validate(request, timing)`
- [x] 2.5 Update the private `Validate(string mode)` method to populate `ContractIds`, `BaselinePath`, and `EnforceUnmatchedIgnoredViolationsPolicy` on the built `ValidationRequest` from the new builder state

## 3. Testing: ArchitectureValidationResult expansion

- [x] 3.1 Add `CoverageFindings`, `CoverageConfig`, `UnmatchedIgnoredViolations`, `UnmatchedIgnoredViolationsConfig`, `CoverageSummaries`, and `Timing` properties to `src/ArchLinterNet.Testing/ArchitectureValidationResult.cs` as optional trailing constructor parameters with backward-compatible defaults
- [x] 3.2 Update `ArchitectureValidationBuilder.Validate(string mode)` to pass the full `ValidationOutcome` shape (plus collected `ValidationTiming`, if any) into the `ArchitectureValidationResult` constructor
- [x] 3.3 Extend `ShouldPass()` to append formatted coverage detail (`FormatCoverageForHumans`) and unmatched-ignored detail (`FormatUnmatchedForHumans`) when those collections are non-empty

## 4. Tests: Testing adapter

- [x] 4.1 Add tests in `tests/ArchLinterNet.Core.Tests/TestingAdapterTests.cs` covering: `WithContracts` restricting execution, `WithBaseline` suppressing a known violation, `WithUnmatchedIgnoredViolationsPolicy` toggled on/off, `WithTimings` populating `Timing`, coverage findings/summaries surfaced on the result, and `ShouldPass()` failure messages including unmatched-ignored/coverage detail
- [x] 4.2 Where a CLI integration test fixture (`tests/ArchLinterNet.Cli.Tests/TestPolicies/*.yml`) already covers an equivalent scenario (contract selection, baseline merge, unmatched-ignore config), mirror that policy's shape in the new Core/Testing tests for shared-fixture parity

## 5. Spec synchronization

- [x] 5.1 Run `openspec validate --all` against the change's delta specs before archiving
- [x] 5.2 Confirm `make fmt` and `make acceptance` pass with the new code and tests

## 6. Archive and PR

- [x] 6.1 Run `opsx-archive` to fold delta specs into `openspec/specs/shared-validation-service/spec.md` and `openspec/specs/test-adapter/spec.md`
- [x] 6.2 Open the PR referencing issue #64

## 7. Code review follow-ups (PR #204)

- [x] 7.1 Add audit-mode test coverage for the new capabilities (`Validate_RequestOverload_AuditMode_SelectsOnlySpecifiedContract` in Core, `ValidateAudit_WithContracts_OnlySelectedContractRuns` in Testing) — acceptance criteria explicitly named `audit` as a required test dimension
- [x] 7.2 Strengthen `Validate_RequestOverload_ConditionSet_ResolvesNamedSet` to assert on a real `#if MY_SYMBOL` branch via a `strict_method_body` contract and `source_roots`, proving condition-set symbol resolution actually changes the outcome instead of only checking the absence of an exception
- [x] 7.3 Rename `ArchitectureValidationBuilder.EnforceUnmatchedIgnoredViolationsPolicy(...)` to `WithUnmatchedIgnoredViolationsPolicy(...)` to match the `With...` naming convention used by every other fluent method on the builder
