## Context

Before this change, `src/ArchLinterNet.Cli/Program.cs` (`RunValidateCommand`, `RunBaselineCommand`), `src/ArchLinterNet.Core/ArchitectureValidator.cs`, and `src/ArchLinterNet.Testing/ArchitectureAssertions.cs` each independently implemented the same pipeline: load the policy YAML, resolve the condition set, resolve the repository root, resolve assemblies, construct an `ArchitectureContractRunner`, select and run contracts for the active mode (`strict`/`audit`), and aggregate violations/cycles. Per `architecture/dependencies.arch.yml`, CLI, Testing, and Unity may depend only on Core, so the shared pipeline has to live in `ArchLinterNet.Core`.

## Goals / Non-Goals

**Goals:**
- One application service, used identically by CLI validation, the public validator API, and the Testing adapter.
- CLI keeps argument parsing and output formatting; no validation logic in the CLI layer beyond that.
- Preserve every existing call site's behavior exactly, including its quirks.

**Non-Goals:**
- YAML schema changes.
- A handler registry for contract families.
- Diagnostics/output-format redesign.
- Performance optimization.
- Unifying the asymmetries between call sites (e.g. making `baseline generate` run `asmdef` checks, or making the public API enforce the unmatched-ignored-violations policy) — these are pre-existing, intentional-by-omission behaviors, not bugs this change is scoped to fix.

## Decisions

**`ArchitectureRunnerFactory` and `ArchitectureContractExecutor` live in `ArchLinterNet.Core.Execution`, not `Validation`.**
Both types expose `ArchitectureContractRunner`-typed members (`ArchitectureRunnerSetup.Runner`, `ArchitectureContractExecutor.Execute`'s `runner` parameter). `ProtectedContractTests` (a Core.Tests characterization test) scans the real Core assembly and treats any non-`Execution`, non-test-area type with a field/property/parameter/return type from a protected namespace as a structural violation. Placing these two types in `Execution` keeps that invariant intact without weakening the test or special-casing it.

**`ValidationRequest` carries explicit flags instead of inferring behavior from caller identity.**
Each existing consumer has a distinct shape: the CLI passes contract-ID filters, a baseline path, and enforces the unmatched-ignored-violations policy; the public API and Testing adapter do neither. Rather than have the service guess based on some "caller type" parameter, `ValidationRequest` exposes `IncludeAsmdefContracts` (defaults to `true`, matching the public API and Testing adapter's existing behavior) and `EnforceUnmatchedIgnoredViolationsPolicy` (defaults to `false`; only the CLI `validate` command opts into it explicitly) so each wrapper states its own requirements directly.

**`ValidationTiming` moved from `ArchLinterNet.Cli` into `ArchLinterNet.Core.Validation`.**
The CLI's `--timings` flag instruments phases (`yaml_loading`, `condition_set_resolution`, per-contract-family counts, etc.) that are now inside the shared service, not in CLI code. Since Core cannot depend on Cli, the timing instrument itself had to move down into Core. `ValidationTiming` has no production-code coupling (just a generic phase stopwatch), so the move has no other contracts that must change.

**`RunBaselineCommand` reuses `ArchitectureRunnerFactory`/`ArchitectureContractExecutor` directly rather than going through `ArchitectureValidationService`.**
Baseline generation's control flow (config-violation early-exit before contract execution; running both `strict` and `audit` modes for `--mode all`; discarding per-contract violations because only `runner.BaselineCandidates` matters) doesn't match `ArchitectureValidationService.Validate`'s single-mode "aggregate into one outcome" shape. Reusing the lower-level factory/executor building blocks removes the duplicated setup code without forcing baseline generation into an orchestration shape it doesn't fit.

## Risks / Trade-offs

[Risk] Silently "fixing" a pre-existing asymmetry while consolidating duplicated code, breaking a caller that depends on the old behavior → Mitigation: every asymmetry was identified against the base branch (via `git show`) before consolidation and preserved through an explicit request flag; characterization tests assert the asymmetry (e.g. `ArchitectureValidatorTests.Validate_FailsPolicyWithViolatedAsmdefContract` failing through the public API, `RunBaselineCommand`'s tests passing unchanged).

[Risk] Misreading the base branch when identifying an asymmetry → Mitigation: this exact failure mode occurred during implementation (the public `ArchitectureValidator` was incorrectly believed to skip `asmdef` checks, when only `baseline generate` does) and was caught in PR review and fixed before merge; the fix added a regression test specifically for that case.

[Risk] `Execution`/`Validation` namespace split feels arbitrary to a future reader → Mitigation: documented above and at the call site (the `ArchitectureContractExecutor`/`ArchitectureRunnerFactory` file comments are intentionally minimal, but this design doc and the `ProtectedContractTests` test name make the constraint discoverable).

## Migration Plan

No migration required — internal refactor only, no schema or API changes. Already implemented and merged via [PR #79](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/79).
