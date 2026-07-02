## 1. Extend the session with state

- [x] 1.1 Add document, selected-contract-id set, unmatched-ignore-tracking flag, and contract catalog fields to `ArchitectureAnalysisSession`.
- [x] 1.2 Move `_unmatchedIgnoredViolations`, `_baselineCandidates`, and `_ruleInputCoveredContractIdsForMode` onto `ArchitectureAnalysisSession`, with `UnmatchedIgnoredViolations`/`BaselineCandidates` read-only accessors.
- [x] 1.3 Move `CreateExecutionContext`, `ResolveContractGroup`, `IsContractSelected`, `PrepareRuleInputCoverageDeferral`, `IsDanglingButCoveredByRuleInputCoverage`, `CollectRuleInputCoveredContractIds` onto the session.
- [x] 1.4 Move the `Strict/AuditXxxContracts()` accessor methods and `CheckConfiguration` onto the session.

## 2. Move contract-family algorithms onto the session

- [x] 2.1 Create `ArchitectureAnalysisSession.Checking.cs`; move every `CheckXxxContract` body from `ArchitectureContractRunner.Checking.cs` onto the session, adjusting field references (`_document` → session field, etc.) with no logic changes.
- [x] 2.2 Create `ArchitectureAnalysisSession.Coverage.cs`; move `BuildCoverageSummary` and all coverage-scope check bodies from `ArchitectureContractRunner.Coverage.cs` onto the session.
- [x] 2.3 Create `ArchitectureAnalysisSession.PolicyConsistency.cs`; move `CheckPolicyConsistency`, `BuildAllDescriptors`, and all `Find*` algorithms from `ArchitectureContractRunner.PolicyConsistency.cs` onto the session.
- [x] 2.4 Delete `ArchitectureContractRunner.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs` once their bodies are fully migrated.

## 3. Update the handler contract to use the session

- [x] 3.1 Change `IArchitectureContractHandler.Execute` from `(ArchitectureContractRunner runner, IArchitectureContract contract)` to `(ArchitectureAnalysisSession session, IArchitectureContract contract)`.
- [x] 3.2 Update every handler in `ArchitectureContractHandlers.cs` to call `session.CheckXxxContract(...)` instead of `runner.CheckXxxContract(...)`.
- [x] 3.3 Update `ArchitectureContractHandlerRegistry.Execute` to accept and forward the session.
- [x] 3.4 Update `ArchitectureContractExecutor.Execute` to take an `ArchitectureAnalysisSession` parameter instead of `ArchitectureContractRunner`, using it for `PrepareRuleInputCoverageDeferral`, `Catalog`, `BuildCoverageSummary`, and handler-registry dispatch.
- [x] 3.5 Update the executor's call sites (`ArchitectureValidationApplicationService`, or wherever `ArchitectureContractExecutor.Execute` is invoked) to pass `runner.Session` instead of `runner`.

## 4. Shrink the runner to a thin facade

- [x] 4.1 Rewrite `ArchitectureContractRunner.cs` so its constructor builds the session with all previously-runner-owned construction parameters, and every existing public member (`UnmatchedIgnoredViolations`, `BaselineCandidates`, `Catalog`, `CheckConfiguration(...)`, `CheckPolicyConsistency()`, every `CheckXxxContract(...)`, every `Strict/AuditXxxContracts()`) becomes a one-line delegation to `_session`.
- [x] 4.2 Keep the internal `Session` property for the executor call-site change in 3.5.
- [x] 4.3 Confirm no public constructor or method signature on `ArchitectureContractRunner` changed.

## 5. Verify behavior compatibility

- [x] 5.1 Run the full existing test suite unchanged and confirm all ~30 direct-runner-call test files still pass without modification.
- [x] 5.2 Add or extend a test proving baseline candidates and unmatched ignores collected during handler dispatch are visible via `runner.BaselineCandidates`/`runner.UnmatchedIgnoredViolations` after the split (session-state visibility through the facade).
- [x] 5.3 Add or extend a test proving the session's type index/reference graph/coverage inventory caches are still created once per run and shared across contract checks within that run.
- [x] 5.4 Run `make fmt` and the project's acceptance suite (`make lint` + `make test` — this repo has no Taskfile, so `make` is the local equivalent); fix any failures.

## 6. Spec sync and archive

- [x] 6.1 Confirm the `contract-handler-execution` delta spec accurately reflects the final `Execute(family, session, contract)` signature.
- [x] 6.2 Run `openspec validate --all` after archiving.
- [x] 6.3 Run `openspec archive shrink-runner-into-session`.
