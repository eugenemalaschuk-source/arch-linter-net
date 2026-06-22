## Why

Validation setup was duplicated across four call sites: the CLI `validate` command, the CLI `baseline generate` command, the public `ArchitectureValidator` API, and the `ArchitectureAssertions` Testing adapter. Each independently loaded the policy YAML, resolved the condition set, resolved the repository root, resolved assemblies, created the contract runner, selected contracts per mode, and aggregated results. This duplication makes new validation capabilities and contract families invasive — every new contract family or setup step has to be wired into four places by hand.

## What Changes

- Introduce `ValidationRequest` / `ValidationOutcome` models describing a validation run and its result.
- Introduce `ArchitectureValidationService` (in `ArchLinterNet.Core.Validation`) that orchestrates the full validate flow: policy load, optional baseline merge, condition-set resolution, repository-root resolution, assembly resolution, runner creation, contract-family selection/execution, and result aggregation.
- Introduce `ArchitectureRunnerFactory` and `ArchitectureContractExecutor` (in `ArchLinterNet.Core.Execution`) as the shared building blocks the service is built from. They live in `Execution` rather than `Validation` because they expose `ArchitectureContractRunner`-typed members, and a characterization test (`ProtectedContractTests`) treats any non-`Execution` type with such a member as a structural violation.
- Move `ValidationTiming` from the CLI project into `ArchLinterNet.Core.Validation` so the shared service can drive the CLI's `--timings` phase breakdown.
- `ArchitectureValidator` (public API) and `ArchitectureAssertions`/`ArchitectureValidationBuilder` (Testing adapter) become thin wrappers over the service with **unchanged public signatures**.
- CLI's `RunValidateCommand` and `RunBaselineCommand` delegate setup/execution to the shared factory/executor; the CLI layer keeps only argument parsing, output formatting, and `--timings` wiring.
- Preserve every existing per-call-site behavioral asymmetry explicitly, via request flags, rather than unifying them:
  - Only `baseline generate` skips `asmdef` contract checks (`ArchitectureValidator` and the Testing adapter both run them, matching base-branch behavior).
  - Only the CLI `validate` command enforces and gates pass/fail on the `analysis.unmatched_ignored_violations` policy.

No **BREAKING** changes — all four public/CLI surfaces are documented as behaviorally unchanged.

## Capabilities

### New Capabilities
- `shared-validation-service`: the internal application-service contract (`ValidationRequest` in, `ValidationOutcome` out) that CLI validation, the public `ArchitectureValidator` API, and the `ArchitectureAssertions` Testing adapter all delegate to for policy load → resolution → contract execution → result aggregation.

### Modified Capabilities
(none — `cli-validation`, `test-adapter`, and `assembly-resolution` describe externally observable behavior, which this change does not alter)

## Impact

- Affected code: `src/ArchLinterNet.Core/ArchitectureValidator.cs`, `src/ArchLinterNet.Testing/ArchitectureAssertions.cs`, `src/ArchLinterNet.Cli/Program.cs`, new files under `src/ArchLinterNet.Core/Validation/` and `src/ArchLinterNet.Core/Execution/`.
- No API signature changes, no YAML schema changes, no CLI argument/exit-code changes.
- Implemented and merged via [PR #79](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/79), closing issue #71. This change documents that work retroactively; tasks below are already complete.
