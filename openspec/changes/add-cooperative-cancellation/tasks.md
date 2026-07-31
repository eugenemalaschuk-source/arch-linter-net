## 1. Core token carriers

- [ ] 1.1 Add `public CancellationToken CancellationToken { get; init; } = default;` to `ValidationRequest`
      (`src/ArchLinterNet.Core/Validation/ValidationRequest.cs`) and `AnalysisSnapshotRequest`
      (`src/ArchLinterNet.Core/Validation/AnalysisSnapshotRequest.cs`).
- [ ] 1.2 Extend `AnalysisSnapshotRequest.ForMode` and `AnalysisSnapshotRequest.FromValidationRequest` to
      carry the token through.

## 2. Snapshot construction and reuse rejection

- [ ] 2.1 In `ArchitectureValidationApplicationService.CreateSnapshotCore`
      (`src/ArchLinterNet.Core/Validation/ArchitectureValidationApplicationService.cs`), call
      `request.CancellationToken.ThrowIfCancellationRequested()` after `LoadDocument`, after
      `ComposeDocument`, after `BuildRunnerFor`, after `RunBuildStatePreflight`, and before/after the
      conditional post-ensure-built rebuild.
- [ ] 2.2 Wire `request.CancellationToken` into the `BuildStatePreflightRequest` constructed in
      `RunBuildStatePreflight` (currently never populated).
- [ ] 2.3 Add a `catch (OperationCanceledException)` block around the setup/preflight/snapshot-construction
      sequence that disposes the partially-built `ArchitectureRunnerSetup setup` (if non-null) before
      rethrowing, and add `OperationCanceledException` to the existing wrap-exclusion list in the outer
      `catch (Exception ex) when (...)` clause alongside `ArchitecturePolicyLoadException`/
      `ArchitecturePolicyValidationException`.
- [ ] 2.4 Add `Cancelled` state to `ArchitectureAnalysisSnapshot`
      (`src/ArchLinterNet.Core/Validation/ArchitectureAnalysisSnapshot.cs`): a private field/public
      property, set inside `Evaluate()`'s catch clause when the caught exception is
      `OperationCanceledException` (excluded from `ArchitectureAnalysisEvaluationException` wrapping the
      same way `ArchitecturePolicyValidationException` already is), and checked at the top of `Evaluate()`
      next to the existing `ObjectDisposedException.ThrowIf` so a cancelled snapshot rejects further
      `Evaluate()` calls.
- [ ] 2.5 Store the request's `CancellationToken` on the snapshot at construction time and check it inside
      `EvaluateCore` at the existing `configuration_check`/`policy_consistency_check`/`contract_checks`/
      `post_processing` timing boundaries.

## 3. Project discovery, assembly resolution, runner setup

- [ ] 3.1 Add a `CancellationToken` parameter to `ArchitectureRunnerSetupService.BuildRunnerCore` (and the
      public `BuildRunner`/`BuildRunnerForPostBuild` methods that call it,
      `src/ArchLinterNet.Core/Execution/ArchitectureRunnerSetupService.cs`), plus the
      `IArchitectureRunnerSetupService` interface.
- [ ] 3.2 Add a `CancellationToken` parameter to `IArchitectureProjectDiscoveryService.ResolveAndApply` and
      its implementation, checked once per discovered project inside the discovery loop.
- [ ] 3.3 Add a `CancellationToken` parameter to `IArchitectureAssemblyResolutionService.Resolve` and
      `ResolvePostBuild` and their implementation(s), checked once per resolved/missing assembly name.
- [ ] 3.4 Update every call site of the three interfaces above (grep for implementers/callers) to pass the
      token through; confirm via `rtk make restore` + a full build that no call site was missed.

## 4. Scanning and fact-index materialization

- [ ] 4.1 Add `CancellationToken CancellationToken { get; }` to `ArchitectureAnalysisContext`, populated at
      construction from the same token threaded through `ArchitectureRunnerSetupService.CreateAnalysisContext`.
- [ ] 4.2 Identify the outer per-file/per-type loop entry points that build each fact index inside the
      `ArchitectureAnalysisSession` partial-class files (type index, IL scan, source-file fact index,
      classification role/metadata indexes) and insert `Context.CancellationToken.ThrowIfCancellationRequested()`
      at each outer loop's top, per design.md Decision 3 (property-on-context, not a parameter on every
      method).

## 5. Contract-family execution

- [ ] 5.1 Add a `CancellationToken` parameter to `IArchitectureContractExecutor.Execute` and
      `ArchitectureContractExecutor.Execute`
      (`src/ArchLinterNet.Core/Execution/ArchitectureContractExecutor.cs`), checked once per
      `session.Catalog.FamiliesInOrder` iteration and once per contract inside both
      `ExecuteStandardFamily` and `ExecuteCoverageFamily`.
- [ ] 5.2 Update `ArchitectureAnalysisSnapshot.EvaluateCore` to pass its stored token into
      `_contractExecutor.Execute(...)`.

## 6. Child build/restore process cancellation

- [ ] 6.1 In `BuildStatePreparationService.RunDotnetCommand`
      (`src/ArchLinterNet.Core/BuildState/BuildStatePreparationService.cs`), replace the blocking
      `process.WaitForExit()` with a bounded poll loop (`while (!process.WaitForExit(pollIntervalMs))`)
      that calls a new `TryKillProcessTree(process)` helper (`process.Kill(entireProcessTree: true)`,
      swallowing a benign "already exited" race) and then `ThrowIfCancellationRequested()` once cancellation
      is observed.
- [ ] 6.2 Confirm the existing `finally` block in `InvokeGraphBuild` that deletes the temp `.slnx` solution
      still runs on the cancellation path (it should, unmodified — verify with a test).
- [ ] 6.3 Confirm `BuildStatePreflightEvaluator.CheckCancellation` and
      `BuildStatePreparationService.EnsureBuilt`'s existing single upfront `ThrowIfCancellationRequested()`
      remain correct/consistent with the new poll-loop check (no double-reporting, no gap between the two).

## 7. Multi-sink report commit

- [ ] 7.1 Add `bool Cancelled` to `RouteResult`
      (`src/ArchLinterNet.Cli/Commands/Validate/ReportCoordinator.cs`).
- [ ] 7.2 Add a `CancellationToken cancellationToken = default` parameter to `RouteSingleOutcome`,
      `RouteCombinedOutcomes`, `RouteErrorToAllSinks`, and the private `RouteOutcomes`/`DistributeToSinks`
      methods.
- [ ] 7.3 In `DistributeToSinks`, check the token before the `StageFileSink` loop begins (if already
      cancelled, skip straight to reporting `Cancelled = true` with no staged/committed paths) and inside
      `CommitPendingRenames`'s loop before each rename.
- [ ] 7.4 On mid-commit cancellation, stop the rename loop, best-effort delete the remaining not-yet-renamed
      staged temp files via the existing `DeleteTempFileBestEffort`, and set `Cancelled = true` on the
      returned `RouteResult` (status is still computed by the existing `BuildRouteResult` logic from
      committed/failed counts — no new `ReportRouteStatus` value).
- [ ] 7.5 Update `ValidateCommandHandler` call sites (`ExecuteSingleMode`, `ExecuteCombinedModes`) to pass
      the CLI's cancellation token into `_coordinator.RouteSingleOutcome`/`RouteCombinedOutcomes`.

## 8. CLI interruption handling and completion status

- [ ] 8.1 Trace the DI wiring from `CliCompositionRoot.Compose()` through `CliRootCommandFactory`/
      `CliCommandModuleCatalog` to wherever `ValidateCommandHandler` is constructed per invocation; add a
      `CancellationTokenSource` at the same composition point `ICliConsole`/`IFileSystem`/`ICliRuntime` are
      already constructed, and thread its `Token` into `ValidateCommandHandler`'s constructor.
- [ ] 8.2 In `CliHost.Run` (or the composition point identified in 8.1), register `Console.CancelKeyPress`
      (`e.Cancel = true` then `cts.Cancel()`) and a `PosixSignalRegistration.Create(PosixSignal.SIGTERM, ...)`
      that also calls `cts.Cancel()`.
- [ ] 8.3 In `ValidateCommandHandler.BuildValidationRequest` and the combined-modes `AnalysisSnapshotRequest`
      construction (`src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandHandler.cs`), populate
      `CancellationToken` from the handler's injected token.
- [ ] 8.4 Add a `catch (OperationCanceledException ex)` branch to `ValidateCommandHandler.Execute`,
      positioned before the existing `catch (Exception ex) when (TryGetPolicyDiagnostic(...))` branch,
      calling a new `WriteCancellation` method (mirroring `WriteExecutionError`'s structure) and returning
      `CliExitCodes.InvalidArgumentsOrRuntimeError`.
- [ ] 8.5 Implement `WriteCancellation`: a `"cancelled"` status literal in JSON (alongside the existing
      `"partial-output"`/`"output-failed"` ad hoc literals), a SARIF result with `ruleId:
      "architecture-cancelled"`, and a plain human-readable cancellation message — reusing
      `NeededErrorFormats`/`WriteErrorContent` unchanged.

## 9. Testing API

- [ ] 9.1 Add `WithCancellation(CancellationToken)` to `ArchitectureValidationBuilder`
      (`src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs`), storing a `_cancellationToken` field,
      matching the existing `WithEnsureBuilt`/`WithNoRestore`/`WithTimings` fluent pattern.
- [ ] 9.2 Thread `_cancellationToken` into the `ValidationRequest`/`AnalysisSnapshotRequest` built by
      `Validate(mode)` and `CreateSnapshot()`.

## 10. Tests

- [ ] 10.1 `ArchitectureAnalysisSnapshot` cancellation: cancelling during `Evaluate("strict")` raises
      `OperationCanceledException` unwrapped; a subsequent `Evaluate("audit")` on the same snapshot also
      raises `OperationCanceledException` without executing audit contracts.
- [ ] 10.2 `ArchitectureValidationApplicationService.CreateSnapshot`/`Validate`: cancelling during policy
      composition, runner setup, and build-state preflight each raise `OperationCanceledException` and never
      return a snapshot; assembly load scope disposal is verified (e.g., via a spy/fake load scope).
- [ ] 10.3 `ArchitectureContractExecutor.Execute`: cancelling between contract-family iterations stops
      further family execution.
- [ ] 10.4 `BuildStatePreparationService`: extend the existing cancellation test pattern (see
      `tests/ArchLinterNet.Core.Tests/BuildStatePreflightTests.cs:287`,
      `Evaluate_CancellationRequested_ReportsCancelled`) with a new test that cancels while a child
      `dotnet` process is running and asserts the process is terminated and the temp `.slnx` solution is
      removed.
- [ ] 10.5 `ReportCoordinator`: a test with multiple file sinks where the token is cancelled between staging
      and commit, and another cancelled mid-commit after one rename — assert `RouteResult.Cancelled`,
      correct `CommittedPaths`, and no orphaned temp files (fake `IFileSystem` call log).
- [ ] 10.6 `ValidateCommandHandler`/CLI integration test: simulate cancellation (inject a pre-cancelled
      token via the test seam used for other CLI integration tests) and assert exit code 2 plus the
      distinct cancelled status in human/JSON/SARIF output.
- [ ] 10.7 `ArchitectureValidationBuilder.WithCancellation`: a Testing-API-level test asserting
      `ValidateStrict()`/`CreateSnapshot()` raise `OperationCanceledException` when the supplied token is
      already cancelled.
- [ ] 10.8 Regression check: run the full existing suite to confirm no behavior change when cancellation is
      never requested (default `CancellationToken.None` throughout).

## 11. Self-architecture and coverage

- [ ] 11.1 Run `rtk make lint-architecture` after the interface/signature changes in sections 3 and 5 to
      confirm no new layering violation was introduced (token-only parameters should not require any new
      cross-layer import).
- [ ] 11.2 Check `architecture/dependencies.arch.yml`'s `self-policy-rule-input-coverage` contract_ids list
      for whether any new file/namespace this change adds needs an entry; add one only if `rtk make
      lint-architecture` actually flags a gap.

## 12. Documentation and spec synchronization

- [ ] 12.1 After implementation and tests are green, compare the delta spec in
      `openspec/changes/add-cooperative-cancellation/specs/cooperative-cancellation/spec.md` against actual
      behavior; adjust wording for anything that changed during implementation (e.g., exact poll interval,
      exact SARIF rule ID) before archiving.
- [ ] 12.2 Run `openspec validate --all` and `openspec archive add-cooperative-cancellation`.
