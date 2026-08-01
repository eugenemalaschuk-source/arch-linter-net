## Why

Every long-running phase of ArchLinterNet — policy composition, project discovery, ensure-built child
process execution, assembly resolution/load, type/IL/source scanning, contract execution, rendering,
and multi-sink report commit — currently runs to completion with no way to stop it early. `CancellationToken`
exists in exactly one narrow slice (`BuildStatePreflightRequest`, from #362) and is inconsistently honored
even there (one call site reports-and-continues, the other throws once and never checks again). The CLI has
no interruption handling at all: Ctrl+C or an external `timeout`/SIGTERM kills the process mid-write, which
can leave a partially-renamed report file, a resolved-but-not-yet-verified build artifact, or an
`ArchitectureAnalysisSnapshot` whose disposal never runs. `ArchLinterNet.Testing`'s long-running entrypoints
(`ValidateStrict`, `CreateSnapshot`, etc.) accept no token at all, so a test harness cannot bound how long a
single assertion may run.

Issue #375 asks for one cooperative cancellation contract, shared by CLI and Testing API, that (a) stops
expensive work promptly, (b) never lets a cancelled or partial operation look successful or become reusable,
and (c) cleans up owned resources (child processes, assembly load scopes, temp files) deterministically. This
must slot into the existing #363 immutable-snapshot and #364 multi-sink-commit machinery without redefining
the cancellation semantics those specs already committed to.

## What Changes

- Add a `CancellationToken` to `ValidationRequest` and `AnalysisSnapshotRequest` (the only new public
  token-carrying surface CLI/Testing touch directly) and thread it through the `core_validation` seam into
  policy composition, build-state preflight, project discovery, assembly resolution, contract-family
  execution, and multi-sink report commit — using `CancellationToken.ThrowIfCancellationRequested()` at
  natural phase/iteration boundaries, matching the codebase's existing fully-synchronous style (no
  async/Task introduced anywhere).
- Give `ArchitectureAnalysisSnapshot` a `Cancelled` state: cancellation observed during any `Evaluate()` call
  is never wrapped by the existing `ArchitectureAnalysisEvaluationException` translation (mirroring how
  `ArchitecturePolicyValidationException` is already excluded), and a cancelled snapshot rejects further
  `Evaluate()` calls instead of silently reusing partial state. A snapshot cancelled during construction is
  never returned to the caller at all.
- Make `BuildStatePreparationService`'s child `dotnet restore`/`dotnet build` invocation cancellation-aware:
  replace the current blocking `Process.WaitForExit()` with a bounded poll loop that kills the process tree
  (`Process.Kill(entireProcessTree: true)`, still no shell involved) when cancellation is observed, while
  keeping the existing temp-solution cleanup.
- Make the CLI's multi-sink two-phase commit (`ReportCoordinator`) cancellation-aware: a `Cancelled` flag is
  added to the existing `RouteResult` (the `AllSucceeded`/`PartialOutput`/`OutputFailed` status model is
  unchanged) so a run interrupted mid-commit reports exactly which files already committed, cleans up
  whatever was still staged, and never claims a rollback of files already renamed into place.
- Add CLI interruption handling: `Console.CancelKeyPress` and a Unix `SIGTERM` `PosixSignalRegistration` both
  trigger one process-scoped `CancellationTokenSource` whose token reaches `ValidateCommandHandler` and
  `ReportCoordinator`. `ValidateCommandHandler` gains a dedicated `catch (OperationCanceledException)` branch
  (ahead of the existing catch-all) that still exits via the existing numeric category 2
  (`CliExitCodes.InvalidArgumentsOrRuntimeError`) but writes a distinct `cancelled` completion status in
  human/JSON/SARIF output.
- Add `ArchitectureValidationBuilder.WithCancellation(CancellationToken)` to `ArchLinterNet.Testing`,
  matching the existing `WithEnsureBuilt`/`WithNoRestore`/`WithTimings` fluent pattern, so Testing API callers
  get equivalent cancellation semantics to the CLI without any Testing-side polling or async surface.
- **BREAKING**: none. Every new member is additive (`init`-only properties defaulting to
  `CancellationToken.None`/`default`, a new builder method, a new `RouteResult` field, a new exception-handling
  branch). Existing non-cancelled call paths are behaviorally unchanged.

## Capabilities

### New Capabilities
- `cooperative-cancellation`: the cross-cutting contract — token propagation scope, precedence of
  cancellation over success, resource-ownership/cleanup rules, and CLI/Testing completion-semantics
  equivalence — that every phase of validation, snapshot creation, and report publication must honor.

### Modified Capabilities
- None. `analysis-build-state-fingerprints` already normatively owns "Snapshot publication is atomic and
  cancellation safe" and "CLI and Testing share ownership semantics" (including snapshot reuse rejection for
  a cancelled snapshot) and already names #375 as a downstream consumer that must reuse, not redefine, that
  contract. This change wires the previously-unpopulated `BuildStatePreflightRequest.CancellationToken` field
  and the snapshot's cancelled-reuse rejection to fulfill that existing contract in code — it does not change
  what that spec requires. `multi-sink-output`'s existing requirements (staged temp files, atomic rename,
  typed `RouteResult` evidence) are similarly extended additively (a new `Cancelled` flag) rather than
  redefined; no existing requirement in that spec changes.

## Impact

- **Core.Validation**: `ValidationRequest`, `AnalysisSnapshotRequest` (new token field + mapper updates),
  `ArchitectureValidationApplicationService.CreateSnapshotCore` (phase-boundary checks, disposal-on-cancel,
  token wired into `BuildStatePreflightRequest`), `ArchitectureAnalysisSnapshot` (new `Cancelled` state,
  `Evaluate()` guard and exception handling).
- **Core.Execution**: `ArchitectureRunnerSetupService`/`IArchitectureRunnerSetupService` and the
  `IArchitectureProjectDiscoveryService`/`IArchitectureAssemblyResolutionService` interfaces they depend on
  (token parameter, checked at per-project/per-assembly boundaries); `ArchitectureContractExecutor` (token
  parameter, checked per contract-family and per contract); `ArchitectureAnalysisContext`/
  `ArchitectureAnalysisSession` (token exposed for the deep scanning/fact-index code, see design.md for the
  scope-limiting tradeoff on how deep this propagates).
- **Core.BuildState**: `BuildStatePreparationService.RunDotnetCommand` (poll-and-kill instead of blocking
  wait); `ArchitectureValidationApplicationService.RunBuildStatePreflight` (token wiring, no signature change
  since `BuildStatePreflightRequest` already has the field).
- **Cli**: `CliHost` (signal registration, `CancellationTokenSource` lifetime), the DI path that constructs
  `ValidateCommandHandler` (token threading), `ValidateCommandHandler` (new catch branch, cancelled status
  content), `ReportCoordinator`/`RouteResult` (token parameter, `Cancelled` flag).
- **Testing**: `ArchitectureValidationBuilder` (new `WithCancellation` method).
- **Tests**: new/updated coverage in `tests/ArchLinterNet.Core.Tests` (snapshot cancellation/reuse rejection,
  contract-executor cancellation, build-state child-process kill) and `tests/ArchLinterNet.Cli.Tests`
  (signal-triggered exit code/status, multi-sink partial-commit-plus-cancelled evidence).
- **No changes** to sequential (non-cancelled) validation identity, ordering, output schema, or exit codes;
  no new CLI flags; no persistent cache or bounded-parallelism work (explicitly out of scope, owned by #365
  and #408 respectively).
