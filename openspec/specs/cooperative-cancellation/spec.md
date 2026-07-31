# cooperative-cancellation Specification

## Purpose
Defines the cross-cutting cooperative cancellation contract shared by the CLI, `ArchLinterNet.Core`,
and `ArchLinterNet.Testing`: how a `CancellationToken` enters the system, which phases observe it,
how cancellation takes precedence over an in-flight but unpublished success, how owned resources
(child build processes, assembly load scopes, staged report files) are cleaned up, and how CLI and
Testing API callers see equivalent completion semantics. This capability builds on — and does not
redefine — the build-state-phase cancellation and snapshot-reuse-rejection rules already owned by
`analysis-build-state-fingerprints`.

## Requirements
### Requirement: One cancellation token enters Core through the validation seam
The system SHALL expose cooperative cancellation to CLI and `ArchLinterNet.Testing` callers exclusively
through a `CancellationToken` carried on `ValidationRequest` and `AnalysisSnapshotRequest`. CLI and Testing
SHALL NOT reach into `Core.Execution`, `Core.Discovery`, `Core.Resolution`, or `Core.Scanning` to pass
cancellation state by any other path.

#### Scenario: CLI validate request carries a token
- **WHEN** the CLI builds a `ValidationRequest` for a `validate` invocation
- **THEN** the request's `CancellationToken` is the token backing the process's Ctrl+C/SIGTERM signal
  registration

#### Scenario: Testing API request carries a caller-supplied token
- **WHEN** `ArchitectureValidationBuilder.WithCancellation(token)` is used before `ValidateStrict()`,
  `ValidateAudit()`, or `CreateSnapshot()`
- **THEN** the resulting `ValidationRequest`/`AnalysisSnapshotRequest` carries that same token

### Requirement: Cancellation propagates through every applicable phase reachable through the validation seam
The system SHALL observe cancellation during policy read/import/composition, project discovery, restore
prerequisite checks and ensure-built child-process execution, build-state hashing/receipt
verification/preflight, assembly resolution/load and snapshot creation, type/IL/source scanning and
fact-index materialization, contract-family execution and coverage/post-processing, and human/JSON/SARIF
rendering and multi-sink staging/validation/commit — for every operation that flows through
`ValidationRequest`/`AnalysisSnapshotRequest` (the `validate` CLI command and the equivalent
`ArchitectureValidationBuilder` Testing API entrypoints).

Baseline operations (`baseline diff`/`verify`/`migrate`/`generate`/`update`/`prune`), public-API operations
(`public-api capture`/`diff`/`update`/`migrate`), and profile-generation/artifact-cleanup operations do NOT
currently accept a `CancellationToken` on their own request types (`BaselineDiffRequest`,
`PublicApiCaptureRequest`, etc.) or thread one through their application services — this capability does not
cover them. Extending coverage to those surfaces is explicit future work, not something this requirement
claims is already true.

#### Scenario: Cancellation during policy composition
- **WHEN** the token is cancelled while `ArchitectureValidationApplicationService.CreateSnapshotCore` is
  composing the policy document
- **THEN** the operation stops before project discovery or assembly resolution begins and raises
  `OperationCanceledException`

#### Scenario: Cancellation during contract-family execution
- **WHEN** the token is cancelled between two contract-family iterations inside
  `ArchitectureContractExecutor.Execute`
- **THEN** no further contract family or contract in that family executes, and no partial violation list
  from the interrupted family is returned as if it were complete

#### Scenario: Cancellation during assembly resolution
- **WHEN** the token is cancelled while resolving target assemblies for a large discovered project graph
- **THEN** resolution stops before the remaining projects are probed and the operation reports cancellation
  rather than a partial resolution result

### Requirement: Cancellation observed before publication wins
The system SHALL treat a cancellation signal observed at any point before a result is fully published
(committed to its output sinks, or returned as a completed `ValidationOutcome`/`ArchitectureAnalysisSnapshot`)
as taking precedence over that result appearing successful. A result that has already been fully published
SHALL NOT be retroactively reclassified by a cancellation signal observed afterward.

#### Scenario: Cancellation right before commit
- **WHEN** validation and rendering complete successfully but the token is cancelled before
  `ReportCoordinator` commits the staged report files
- **THEN** the invocation reports cancellation, not success, and no unintended file is left committed beyond
  what the commit loop had already renamed at the moment cancellation was observed

#### Scenario: Cancellation after successful completion has no effect
- **WHEN** a `ValidateCommandHandler.Execute` call has already returned a successful exit code and the
  process's token source is cancelled afterward (e.g., a late Ctrl+C after output was already flushed)
- **THEN** the already-returned result is unaffected; there is no code path that revisits a completed
  invocation's exit code based on a later cancellation signal

### Requirement: A snapshot cancelled during construction is never exposed as usable
The system SHALL NOT return an `ArchitectureAnalysisSnapshot` instance from snapshot creation if cancellation
was observed at any point during policy composition, runner setup, or build-state preflight for that
snapshot. Resources already acquired during the interrupted construction (assembly load scope, session
context) SHALL be disposed before the cancellation exception propagates to the caller.

#### Scenario: Cancellation during runner setup
- **WHEN** the token is cancelled while `ArchitectureRunnerSetupService.BuildRunnerCore` is resolving target
  assemblies
- **THEN** `ArchitectureValidationApplicationService.CreateSnapshot`/`Validate` raises
  `OperationCanceledException` and no `ArchitectureAnalysisSnapshot` is constructed or returned

#### Scenario: Partially loaded assemblies are released
- **WHEN** cancellation interrupts snapshot construction after an isolated assembly load scope was created
  but before the snapshot is returned
- **THEN** that load scope is disposed (its `AssemblyLoadContext` unloaded) as part of unwinding the
  cancellation, not left to finalization

### Requirement: A cancelled snapshot rejects further reuse
The system SHALL reject any further `Evaluate()` call on an `ArchitectureAnalysisSnapshot` once cancellation
has been observed during a prior `Evaluate()` call on that same snapshot, in addition to the existing
rejection of calls on a disposed snapshot.

#### Scenario: Second mode evaluated after the first was cancelled
- **WHEN** `Evaluate("strict")` on a shared snapshot observes cancellation, and the caller then calls
  `Evaluate("audit")` on the same snapshot instance
- **THEN** the second call raises `OperationCanceledException` immediately without evaluating `audit`
  contracts against the snapshot's session

#### Scenario: Cancellation is not wrapped as an evaluation error
- **WHEN** `Evaluate()` observes an `OperationCanceledException` from a contract check
- **THEN** it re-raises that exception directly and does not wrap it in
  `ArchitectureAnalysisEvaluationException`, mirroring how `ArchitecturePolicyValidationException` is already
  excluded from that wrapping

### Requirement: Child build/restore processes are cancelled without a shell and without leaking resources
The system SHALL terminate an in-flight `dotnet restore`/`dotnet build` child process (started under
ensure-built preparation) when cancellation is observed, without invoking a shell to do so, and SHALL still
remove the temporary graph solution file that preparation created for that invocation.

#### Scenario: Cancellation while the child build is running
- **WHEN** the token is cancelled while `BuildStatePreparationService` is waiting for a `dotnet build` child
  process to exit
- **THEN** the process (and any of its child processes) is terminated, `OperationCanceledException`
  propagates, and no shell process was ever started to perform the termination

#### Scenario: Temp solution file cleanup still runs
- **WHEN** cancellation interrupts `InvokeGraphBuild` after the temporary `.slnx` solution file was written
- **THEN** that temporary file is deleted before the cancellation exception reaches the caller

### Requirement: Multi-sink commit reports typed partial-output evidence plus cancellation
The system SHALL, when cancellation is observed during multi-sink report staging or commit, report the same
typed partial-output evidence (`CommittedPaths`, `StagedPaths`, `UncommittedPaths`, `FailedPaths`) the
existing partial-output/output-failed paths already report, additionally marked as cancelled. Files already
renamed into their target location before cancellation was observed SHALL remain committed; the system SHALL
NOT attempt to undo or roll back an already-committed file.

#### Scenario: Cancellation before any file commits
- **WHEN** cancellation is observed before `ReportCoordinator.CommitPendingRenames` begins
- **THEN** the returned result has zero committed paths, is marked cancelled, and every staged temp file has
  been removed

#### Scenario: Cancellation mid-commit with one sink already renamed
- **WHEN** two file sinks are staged and cancellation is observed after the first has been renamed into
  place but before the second is processed
- **THEN** the returned result lists the first sink's target path as committed, is marked cancelled, reports
  the second sink as uncommitted, and the second sink's temp file has been removed rather than left as an
  orphaned `.tmp` artifact or renamed

### Requirement: CLI and Testing API expose equivalent cancellation completion semantics
The system SHALL treat CLI process interruption (Ctrl+C, SIGTERM) and a Testing API caller-supplied
`CancellationToken` as equivalent triggers of the same underlying cancellation contract: both surface as
`OperationCanceledException` through the `Core.Validation` seam, and both result in the same "no partial
success, no reusable partial state" guarantees.

#### Scenario: CLI Ctrl+C during validation
- **WHEN** a user presses Ctrl+C while `arch-linter-net validate` is running
- **THEN** the process exits with `CliExitCodes.InvalidArgumentsOrRuntimeError` (2) and a distinct
  `cancelled` completion status is written to the configured output format, without requiring a TTY to have
  produced that output

#### Scenario: Testing API token cancellation during an assertion
- **WHEN** a test cancels the `CancellationToken` passed to `WithCancellation` while `ValidateStrict()` is
  executing
- **THEN** `ValidateStrict()` raises `OperationCanceledException` to the test, with no `ArchitectureValidationResult`
  returned

### Requirement: Cancellation completion status is distinct from other CLI outcomes
The system SHALL report a cancelled CLI invocation with a completion status distinguishable, in every
supported output format, from a validation failure, an invalid-input error, a configuration/preflight
failure, a build failure, and an unexpected tool failure, while still exiting through the existing
`CliExitCodes.InvalidArgumentsOrRuntimeError` numeric category.

#### Scenario: JSON output marks cancellation distinctly
- **WHEN** `--format json` is used and the invocation is cancelled after policy load succeeded
- **THEN** the emitted JSON document has a status/kind field whose value is distinct from the existing
  `architecture_execution_error` and `partial-output`/`output-failed` values already used for other failure
  categories

#### Scenario: SARIF output marks cancellation distinctly
- **WHEN** `--format sarif` is used and the invocation is cancelled
- **THEN** the emitted SARIF document's result carries a rule identifier distinct from the existing
  `architecture-execution`/`architecture-policy` identifiers

