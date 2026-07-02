## Why

`ArchitectureContractRunner` and its partials (`.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs`, ~2,428 lines combined) remain the primary home of every contract-family algorithm body, even though #137 routed every family through `IArchitectureContractHandler`. Each handler's `Execute(ArchitectureContractRunner runner, IArchitectureContract contract)` still delegates straight back to `runner.CheckXxxContract(...)`, so the runner is still a god object holding both the algorithms and all per-run mutable state (unmatched-ignore tracking, baseline candidates, rule-input coverage deferral, catalog, document, selection filtering). Issue #138 asks to move the algorithms into handlers/checkers and scope the shared mutable state into an explicit session/context object, so handlers receive explicit inputs instead of the runner itself.

## What Changes

- `ArchitectureAnalysisSession` (currently owning only `Context`/`TypeIndex`/`ReferenceGraph`/coverage-inventory cache) is extended to become the validation session/context the issue describes. It gains the document reference, selected-contract-id set, unmatched-ignore-tracking flag, the contract catalog, the `_unmatchedIgnoredViolations` and `_baselineCandidates` lists, `_ruleInputCoveredContractIdsForMode`, `CreateExecutionContext`, `ResolveContractGroup`, `IsContractSelected`, the rule-input coverage deferral methods, the `Strict/AuditXxxContracts()` accessors, and `CheckConfiguration`.
- All `CheckXxxContract` algorithm bodies currently in `ArchitectureContractRunner.Checking.cs`, `.Coverage.cs`, and `.PolicyConsistency.cs` move onto `ArchitectureAnalysisSession` as new partial files, operating on the session's own fields.
- `IArchitectureContractHandler.Execute` changes from `(ArchitectureContractRunner runner, IArchitectureContract contract)` to `(ArchitectureAnalysisSession session, IArchitectureContract contract)`. **BREAKING** (internal API only — `IArchitectureContractHandler` is an `internal` interface with no external consumers). `ArchitectureContractHandlerRegistry.Execute` and all handlers in `ArchitectureContractHandlers.cs` update accordingly.
- `ArchitectureContractExecutor.Execute` takes an `ArchitectureAnalysisSession` instead of an `ArchitectureContractRunner`.
- `ArchitectureContractRunner` shrinks to a thin facade: it constructs the session and exposes its existing public surface (`UnmatchedIgnoredViolations`, `BaselineCandidates`, `Catalog`, `CheckConfiguration(...)`, `CheckPolicyConsistency()`, every `CheckXxxContract(...)` method, every `StrictXxxContracts()/AuditXxxContracts()` accessor) as one-line delegations to the session. The public constructor signature and every public method signature are unchanged, so existing call sites (`ArchitectureValidationApplicationService`, `ArchitectureBaselineApplicationService`, and ~30 test files that construct `new ArchitectureContractRunner(...)` and call `CheckXxxContract`/read `BaselineCandidates`/`UnmatchedIgnoredViolations` directly) require no changes.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `contract-handler-execution`: the handler/registry dispatch signature changes from `(family, runner, contract)` to `(family, session, contract)` — handlers receive the validation session/context instead of the runner.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs` and new partials `ArchitectureAnalysisSession.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs` (grows significantly — gains the algorithm bodies and mutable state).
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.cs`, `.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs` (shrink to thin delegation).
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandler.cs`, `ArchitectureContractHandlers.cs`, `ArchitectureContractHandlerRegistry.cs`, `ArchitectureContractExecutor.cs` (signature change from runner to session).
- No changes to `ServiceCollectionExtensions.AddArchLinterNetCore()`, `ArchitectureRunnerSetupService`, `ArchitectureValidationApplicationService`, `ArchitectureBaselineApplicationService`, or any test file's call sites — the runner's public API is preserved.
- Tests: existing suite proves behavior compatibility unchanged; add/extend tests proving session-owned state (baseline candidates, unmatched ignores, type index/reference graph, coverage inventory) is correctly scoped to the session and shared correctly across handler dispatch within one run.
