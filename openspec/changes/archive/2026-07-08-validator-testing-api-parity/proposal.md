## Why

The CLI's `validate` command already supports mode selection (strict/audit), contract-ID selection, condition sets, baseline merging, unmatched-ignored-violation enforcement, and timings, all backed by the shared `ValidationRequest`/`ValidationOutcome`/`ArchitectureEngine` pipeline (see `shared-validation-service`). The two public convenience surfaces that .NET users actually call from code — `ArchitectureValidator` (Core) and `ArchitectureValidationBuilder`/`ArchitectureAssertions` (Testing) — only thread through a small subset of that pipeline (policy path, preprocessor symbols, condition set) and only return a subset of the outcome (violations, cycles, policy-consistency findings). Teams that gate architecture policy from NUnit/xUnit/MSTest, or from custom in-process tooling, cannot select contracts, merge a baseline, enforce unmatched-ignored-violation policy, inspect coverage findings, or collect timings without reaching into the internal `ArchitectureEngineBuilder` composition root themselves. Issue #64 closes that parity gap.

## What Changes

- Add `ArchitectureValidator.Validate(ValidationRequest request, ValidationTiming? timing = null)`, a new overload that delegates directly to the already-composed `ArchitectureEngine.Validate` and returns the full `ValidationOutcome` (violations, cycles, coverage findings/summaries, unmatched-ignored violations + config, policy-consistency findings + config, pass/fail). The 3 existing `Validate(...)` overloads are unchanged in behavior and signature — fully backward compatible.
- Extend `ArchitectureValidationBuilder` (Testing) with fluent methods mirroring `ValidationRequest`: `WithContracts(...)` (contract-ID selection), `WithBaseline(string baselinePath)` (baseline merge into the validation run), `EnforceUnmatchedIgnoredViolationsPolicy(bool enforce = true)`, and `WithTimings()`. `WithConditionSet`, `ValidateStrict()`, and `ValidateAudit()` keep their existing signatures and defaults.
- Extend `ArchitectureValidationResult` (Testing) to carry the remaining `ValidationOutcome` fields: `CoverageFindings`, `CoverageConfig`, `UnmatchedIgnoredViolations`, `UnmatchedIgnoredViolationsConfig`, `CoverageSummaries`, and a nullable `Timing`. `ShouldPass()`'s failure message is extended to report coverage and unmatched-ignored detail when present, reusing the existing `ArchitectureDiagnosticFormatter` methods.
- **Scope note (not a breaking change, but worth calling out):** baseline *lifecycle* operations (generate/update/prune/diff/verify, added in #63) are explicitly **not** given new Core/Testing convenience wrappers in this change. Only baseline-*merge*-into-a-validation-run (`ValidationRequest.BaselinePath`, matching CLI's `validate --baseline`) is in scope, since that's the one baseline capability the `validate` command itself exposes. The lifecycle commands remain reachable through `ArchitectureEngine` for advanced integrators.

## Capabilities

### New Capabilities
(none — this change extends two existing capabilities)

### Modified Capabilities
- `shared-validation-service`: add a requirement that `ArchitectureValidator` exposes a `ValidationRequest`-based overload delegating to `ArchitectureEngine.Validate` and returning the unmodified `ValidationOutcome`.
- `test-adapter`: add requirements for contract-ID selection, baseline merge, unmatched-ignored-violation enforcement toggle, timings collection, and the expanded `ArchitectureValidationResult` shape (coverage findings/summaries, unmatched-ignored data, timing).

## Impact

- `src/ArchLinterNet.Core/ArchitectureValidator.cs` — new overload, no changes to existing members.
- `src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs` — new fluent methods, existing methods unchanged.
- `src/ArchLinterNet.Testing/ArchitectureValidationResult.cs` — new properties (optional constructor parameters, backward compatible), extended `ShouldPass()` message.
- `tests/ArchLinterNet.Core.Tests/ArchitectureValidatorTests.cs`, `TestingAdapterTests.cs` — new test coverage for strict, audit, selected-contract, condition-set, baseline-merge, unmatched-ignore (error/warn/off), coverage, timings, and backward-compatible overload cases.
- `openspec/specs/shared-validation-service/spec.md`, `openspec/specs/test-adapter/spec.md` — updated requirements.
- No changes to CLI behavior, YAML policy schema, or NuGet package dependencies.
