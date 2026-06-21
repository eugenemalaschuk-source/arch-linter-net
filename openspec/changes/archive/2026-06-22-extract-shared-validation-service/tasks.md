## 1. Shared building blocks (Core)

- [x] 1.1 Add `ValidationRequest` and `ValidationOutcome` to `ArchLinterNet.Core.Validation`
- [x] 1.2 Move `ValidationTiming` from `ArchLinterNet.Cli` into `ArchLinterNet.Core.Validation`
- [x] 1.3 Add `ArchitectureRunnerFactory` (`LoadDocument`, `BuildRunner`) to `ArchLinterNet.Core.Execution`
- [x] 1.4 Add `ArchitectureContractExecutor.Execute` (per-mode contract-family execution + layer-template expansion + aggregation) to `ArchLinterNet.Core.Execution`
- [x] 1.5 Add `ArchitectureValidationService.Validate` orchestrating the full flow, including unknown-contract-ID validation and the unmatched-ignored-violations policy gate

## 2. Wire up consumers

- [x] 2.1 Rewrite `ArchitectureValidator` as a thin wrapper over `ArchitectureValidationService` (unchanged public signatures)
- [x] 2.2 Rewrite `ArchitectureAssertions`/`ArchitectureValidationBuilder` as a thin wrapper over the service (unchanged public signatures)
- [x] 2.3 Rewrite CLI `RunValidateCommand` to delegate to `ArchitectureValidationService`, keeping only arg parsing, output formatting, and `--timings` wiring
- [x] 2.4 Rewrite CLI `RunBaselineCommand` to reuse `ArchitectureRunnerFactory`/`ArchitectureContractExecutor`, keeping its config-violation early exit and dual strict+audit execution for `--mode all` in the CLI layer

## 3. Preserve existing asymmetries

- [x] 3.1 Verify against the base branch which call sites skip `asmdef` contracts and which enforce the unmatched-ignored-violations policy (`git show <base-sha>:<path>`)
- [x] 3.2 Encode those asymmetries as `ValidationRequest.IncludeAsmdefContracts` / `EnforceUnmatchedIgnoredViolationsPolicy`, defaulting to the CLI `validate` command's fuller behavior
- [x] 3.3 Fix incorrect `IncludeAsmdefContracts = false` on `ArchitectureValidator` found during PR review (base branch did run asmdef checks there) and add a regression test

## 4. Characterization tests

- [x] 4.1 Add `ArchitectureValidatorTests.Validate_FailsPolicyWithViolatedContract` (failing-policy case for the public API)
- [x] 4.2 Add `ArchitectureValidatorTests.Validate_FailsPolicyWithViolatedAsmdefContract` (asmdef regression test for the public API)
- [x] 4.3 Update `LayerTemplateContractTests.CheckLayerContract_Exhaustive_AllChildrenMapped_NoViolation` to include the new `ArchLinterNet.Core.Validation` namespace

## 5. Validation

- [x] 5.1 `make fmt` — no diff
- [x] 5.2 `make acceptance` — all Core, Cli, and Unity tests passing
- [x] 5.3 `make lint-architecture` — passing (no new cross-package or protected-surface violations)
- [x] 5.4 Open PR #79, address review feedback, push fix
