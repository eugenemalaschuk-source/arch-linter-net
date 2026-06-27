## 1. Break the existing internal cycle

- [x] 1.1 Move `ValidationTiming` from `Core.Validation` to `Core.Reporting` to eliminate the `Core.Validation` ↔ `Core.Execution` namespace cycle.
- [x] 1.2 Update `using` directives in `ArchitectureContractExecutor`, `ArchitectureRunnerFactory`, `ArchitectureValidationService`, and `Program.cs` accordingly.

## 2. Extract the baseline application seam

- [x] 2.1 Add `BaselineGenerationRequest` and `BaselineGenerationOutcome` in `src/ArchLinterNet.Core/Validation/`.
- [x] 2.2 Add `ArchitectureBaselineService.Generate(...)` wrapping the existing load/build-runner/check-configuration/execute/generate/serialize flow.
- [x] 2.3 Update `Program.cs`'s `RunBaselineCommand` to call `ArchitectureBaselineService` instead of `Core.Execution`/`Core.Contracts` directly.
- [x] 2.4 Remove now-unused `Core.Execution`/`Core.Contracts` `using` directives from `Program.cs`.

## 3. Self-policy

- [x] 3.1 Add `core_model`, `core_reporting`, `core_resolution`, `core_contracts`, `core_execution`, `core_validation` layers to `architecture/dependencies.arch.yml`.
- [x] 3.2 Add strict contracts: CLI-must-use-seam, execution-must-not-depend-on-cli-or-testing, reporting-stays-a-leaf, model-stays-independent, resolution-must-not-depend-upward, scanning-must-not-depend-upward.
- [x] 3.3 Add `core-application-seam-layering` to `strict_layers`.
- [x] 3.4 Add `src/ArchLinterNet.Cli/bin/Debug/net10.0` to `assembly_search_paths`.

## 4. Wire self-validation into the lint gate

- [x] 4.1 Add `SelfArchitecturePolicyTests.cs` to `tests/ArchLinterNet.Core.Tests/`, validating the real repository policy in strict mode via `ArchitectureAssertions.FromRepositoryRoot`.
- [x] 4.2 Update `make/lint.mk`'s `lint-architecture` target to build `Cli` and `Unity` first so all `target_assemblies` resolve.

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `make acceptance` (no Taskfile present in this repo) and resolve failures — passed (lint + 427 tests).
- [x] 5.3 Run `openspec validate --all` after archiving the change.
