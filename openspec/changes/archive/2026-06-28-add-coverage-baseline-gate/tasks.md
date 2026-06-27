## 1. Model changes

- [x] 1.1 Add `IgnoredViolations` (`ignored_violations`) property to `ArchitectureCoverageContract` in `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`, matching the shape used by other contract types.
- [x] 1.2 Add `StrictCoverage`/`AuditCoverage` (`strict_coverage`/`audit_coverage`) properties to `ArchitectureBaselineContractGroups` in `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineModels.cs`.

## 2. Baseline plumbing

- [x] 2.1 Add `strict_coverage`/`audit_coverage` cases to `ArchitectureBaselineGenerator.SetGroupEntries`.
- [x] 2.2 Add `ValidateGroupEntries` calls for `strict_coverage`/`audit_coverage` in `ArchitectureBaselineLoader.Validate`.
- [x] 2.3 Add `strict_coverage`/`audit_coverage` to `ArchitectureBaselineMerger.Merge` and `MergeAndValidate`, including `ContractGroupMerger.GetContracts` (reading `ArchitectureContractGroups.StrictCoverage`/`AuditCoverage`) and `GetIgnoredViolations` (`ArchitectureCoverageContract c => c.IgnoredViolations`).

## 3. Runner integration

- [x] 3.1 In `CheckCoverageContract` (`ArchitectureContractRunner.Coverage.cs`), call `CreateExecutionContext(contract, contract.IgnoredViolations)` and gate each emitted "uncovered namespace" finding behind `executionContext.IsIgnored(namespace, "uncovered namespace")`; call `executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations)` before returning.
- [x] 3.2 In `CheckRuleInputCoverageContract`, apply the same `CreateExecutionContext`/`IsIgnored`/`CollectUnmatchedIgnores` pattern to the `"unresolved"` and `"empty-input"` findings.
- [x] 3.3 Confirm `ArchitectureContractCatalog.ResolveGroup` already returns `strict_coverage`/`audit_coverage` for these contracts (no change expected) so baseline candidates land in the correct group.
- [x] 3.4 Ensure `ArchitectureBaselineService.Generate` includes coverage contract execution when building `runner.BaselineCandidates` (verify `ArchitectureContractExecutor.Execute` already runs the `"coverage"` family for `strict`/`audit` modes during baseline generation; adjust only if candidates are missing).

## 4. Tests

- [x] 4.1 Unit tests in `tests/ArchLinterNet.Core.Tests/ArchitectureBaselineGeneratorTests.cs` (or a new coverage-focused file) covering: initial baseline generation from uncovered namespaces and rule-input findings, deterministic output, manual `ignored_violations` not duplicated.
- [x] 4.2 Unit tests covering baseline-suppression: a previously-baselined uncovered namespace/rule reference is suppressed; a new uncovered item not in the baseline still fails.
- [x] 4.3 Unit tests covering stale baseline entries: a baselined coverage finding that has been resolved is reported via `unmatched_ignored_violations`.
- [x] 4.4 Unit tests confirming coverage baseline entries never suppress or interact with ordinary `strict`/`audit` dependency violations.
- [x] 4.5 Unit tests confirming `audit_coverage` baseline behavior matches existing audit (non-blocking) semantics.
- [x] 4.6 CLI integration tests in `tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.BaselineGenerate.cs` / `CliIntegrationTests.BaselineValidation.cs` (or new coverage-specific variants) exercising `baseline generate` and `validate --baseline` end-to-end against a policy with coverage contracts.
- [x] 4.7 Loader/merger tests confirming old baseline files without `strict_coverage`/`audit_coverage` keys still load and validate unchanged.

## 5. Docs and spec sync

- [x] 5.1 Update `docs/guides/migration-baselines.md` to describe coverage baseline adoption.
- [x] 5.2 Update `docs/contracts/coverage.md` to describe `ignored_violations`/baseline support for coverage contracts.
- [ ] 5.3 Run `openspec validate --all` after archiving to confirm specs are consistent.

## 6. Validation

- [x] 6.1 Run `make fmt`.
- [x] 6.2 Run `make acceptance` (this repo has no Taskfile; `task acceptance:fresh` is not applicable) and resolve any failures.
