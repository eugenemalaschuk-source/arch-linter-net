## 1. Core: snapshot request and counters
- [x] 1.1 Add `AnalysisSnapshotRequest` record in `src/ArchLinterNet.Core/Validation/` mirroring `ValidationRequest` minus `Mode`, with `ForMode(string mode)` producing a `ValidationRequest`.
- [x] 1.2 Add `ArchitectureAnalysisSnapshotCounters` record (`PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, `ModesEvaluated`, all `int`).

## 2. Core: snapshot type and refactored application service
- [x] 2.1 Extract the per-mode evaluation tail of `ArchitectureValidationApplicationService.Validate` (configuration check, policy-consistency check, contract execution, unmatched-ignored resolution, coverage filtering, classification checks) into a reusable internal method.
- [x] 2.2 Add `ArchitectureAnalysisSnapshot : IDisposable` in `src/ArchLinterNet.Core/Validation/` owning the composed document, `ArchitectureRunnerSetup`, preflight result, and counters; `Evaluate(mode, timing)` memoizes per-mode `ValidationOutcome`; blocked preflight short-circuits every mode; disposed snapshot throws `ObjectDisposedException` on `Evaluate`.
- [x] 2.3 Add `CreateSnapshot(AnalysisSnapshotRequest, ValidationTiming?)` to `IArchitectureValidationApplicationService` and implement it in `ArchitectureValidationApplicationService`, reusing the existing setup/preflight/`--ensure-built` re-setup sequence unchanged.
- [x] 2.4 Reimplement `Validate(ValidationRequest, ValidationTiming?)` on top of `CreateSnapshot` + `Evaluate` + `Dispose`, preserving its exact current signature and behavior. (A `modeHint` parameter was threaded through the internal setup path so the single-mode `Validate` path keeps the exact original mode-scoped assembly-resolution/coverage-bypass behavior, while the public `CreateSnapshot` uses the existing "mode=null means union of strict+audit" semantics for snapshots meant to serve more than one mode.)

## 3. Testing API
- [x] 3.1 Add an explicit shared-snapshot entry point to `ArchitectureValidationBuilder` (or a small wrapper type) that owns one `ArchitectureAnalysisSnapshot` and exposes `ValidateStrict()`/`ValidateAudit()` evaluated against it, implementing `IDisposable`. (`ArchitectureValidationBuilder.CreateSnapshot()` returns `ArchitectureValidationSnapshotSession`.)
- [x] 3.2 Leave existing `ArchitectureValidationBuilder.ValidateStrict()`/`ValidateAudit()` behavior unchanged.

## 4. CLI
- [x] 4.1 Extend `ValidateCommandHandler`'s mode handling to accept a comma-separated `--mode` list, validating each entry against `strict`/`audit`.
- [x] 4.2 For more than one requested mode, build one snapshot via `CreateSnapshot`, evaluate each requested mode against it in order, report each mode's outcome, and fail if any mode fails.
- [x] 4.3 Keep single-value `--mode` behavior and output unchanged.

## 5. Tests
- [x] 5.1 Core tests: snapshot composes once and serves strict+audit with identical results to two independent `Validate` calls (single-project and multi-project fixtures). (`ArchitectureAnalysisSnapshotTests`, fake-composition seam.)
- [x] 5.2 Core tests: repeated `Evaluate` for the same mode returns a memoized outcome; disposed snapshot throws on `Evaluate`; blocked preflight short-circuits every mode.
- [x] 5.3 Core tests: `Counters` reflect one composition/evaluation and the number of modes evaluated.
- [x] 5.4 Testing API tests: shared-snapshot wrapper evaluates strict/audit against one snapshot and disposes deterministically; existing `ValidateStrict`/`ValidateAudit` tests remain green. (`ArchitectureValidationSnapshotSessionTests`.)
- [x] 5.5 CLI tests: `--mode strict,audit` builds one snapshot and reports both outcomes; single-mode invocation output/exit-code unchanged. (`CombinedMode_*` tests in `CliIntegrationTests.cs`.)

## 6. Validation
- [x] 6.1 Run `make fmt`.
- [x] 6.2 Run `make acceptance` (lint-code-size, lint-dotnet-format, lint-architecture, all tests) and fix issue-related failures. (Fixed a real regression this change introduced: passing `mode=null` unconditionally into the existing mode-aware assembly-resolution/coverage-bypass logic broke a strict-mode-only test; fixed by threading a `modeHint` through so single-mode `Validate` keeps its exact original mode-scoped behavior.)

## 7. Spec sync and archive
- [x] 7.1 Compare implementation against this proposal/design/spec; update the delta spec if behavior differs. (Implementation matches; no delta spec changes needed.)
- [x] 7.2 Run `openspec validate --all`.
- [x] 7.3 Run `openspec archive add-analysis-snapshot`.
