## MODIFIED Requirements

### Requirement: Cancellation propagates through every applicable phase reachable through the validation seam
The system SHALL observe cancellation during policy read/import/composition, project discovery, restore
prerequisite checks and ensure-built child-process execution, build-state hashing/receipt
verification/preflight, assembly resolution/load and snapshot creation, type/IL/source scanning and
fact-index materialization, contract-family execution and coverage/post-processing, and human/JSON/SARIF
rendering and multi-sink staging/validation/commit — for every operation that flows through
`ValidationRequest`/`AnalysisSnapshotRequest` (the `validate` CLI command and the equivalent
`ArchitectureValidationBuilder` Testing API entrypoints).

Baseline operations (`baseline diff`/`verify`/`migrate`/`generate`/`update`/`prune`) and public-API operations
(`public-api capture`/`diff`/`update`/`migrate`) carry a `CancellationToken` on their own request types
(`BaselineDiffRequest`, `PublicApiCaptureRequest`, etc.), observe it immediately before their own
write/rename publication step, and report a distinct typed `cancelled` completion instead of folding
`OperationCanceledException` into a generic error — see "Shared application seams observe cancellation"
below for the precise publication-safety guarantee these surfaces provide.

Profile-generation and artifact-cleanup operations do not exist in the codebase yet — they depend on the
`analysis-profile/v1` contract (issue #374), which has not landed — so there is no implementation for this
capability to add cancellation checks to. Extending coverage to those surfaces once they exist is tracked by
issue #418, not something this requirement claims is already true.

#### Scenario: Cancellation during policy composition
- **WHEN** the token is cancelled while `ArchitectureValidationApplicationService.CreateSnapshotCore` is
  composing the policy document
- **THEN** the operation stops before project discovery or assembly resolution begins and raises
  `OperationCanceledException`

#### Scenario: Cancellation during a deep policy import graph
- **WHEN** the token is cancelled while `ArchitecturePolicyImportGraphResolver` is traversing a multi-file
  import chain
- **THEN** the current document's own import list stops being visited and no further import file is read

#### Scenario: Cancellation during deep type/role scanning
- **WHEN** the token is cancelled while `ArchitectureTypeIndex.LoadAllTypes()` or `ArchitectureRoleIndex.BuildData()`
  is scanning a large target-assembly set
- **THEN** scanning stops at the next assembly/type boundary and raises `OperationCanceledException`

#### Scenario: Cancellation during contract-family execution
- **WHEN** the token is cancelled between two contract-family iterations inside
  `ArchitectureContractExecutor.Execute`
- **THEN** no further contract family or contract in that family executes, and no partial violation list
  from the interrupted family is returned as if it were complete

#### Scenario: Cancellation during assembly resolution
- **WHEN** the token is cancelled while resolving target assemblies for a large discovered project graph
- **THEN** resolution stops before the remaining projects are probed and the operation reports cancellation
  rather than a partial resolution result

#### Scenario: Cancellation during build-input fingerprint hashing
- **WHEN** the token is cancelled while `BuildStateCanonicalHasher.ComputeBuildInputFingerprint` is hashing a
  project's source/import files
- **THEN** hashing stops before the remaining files are read and no build receipt is written from a partial
  fingerprint

### Requirement: Cancellation completion is complete and non-retroactive
The system SHALL retain every configured file sink in cancellation evidence, SHALL not reclassify fully delivered streams after publication, and SHALL drain asynchronous child output before reading diagnostics.

#### Scenario: Cancellation before rendering
- **WHEN** cancellation is observed before report rendering with file sinks configured
- **THEN** every configured file destination is reported as uncommitted

#### Scenario: Child process exits during polling
- **WHEN** a child process exits while async stdout or stderr callbacks remain pending
- **THEN** diagnostic output is read only after parameterless `WaitForExit()` completes

#### Scenario: Cancellation mid-render stops before the next section or mode
- **WHEN** cancellation is observed while `ReportCoordinator` is rendering one human report section (e.g.
  between violations and cycles) or one mode of a combined strict+audit JSON/SARIF document
- **THEN** rendering stops before the next section or mode, and `OperationCanceledException` propagates
  instead of a partial document reaching any sink

#### Scenario: A killed build/restore process that will not confirm exit is reported, not silently leaked
- **WHEN** `BuildStatePreparationService` kills a cancelled child `dotnet build`/`dotnet restore` process and
  it does not report exit within the bounded post-kill deadline
- **THEN** the operation raises a typed cleanup-timeout exception (a subtype of `OperationCanceledException`)
  identifying the process instead of silently treating the kill as confirmed

### Requirement: Shared application seams observe cancellation
Baseline, public-API, policy composition, hashing, receipt publication, and final outcome construction SHALL observe the caller token before publishing a completed result.

#### Scenario: Cancellation during shared pipeline work
- **WHEN** the caller cancels during one of the shared pipeline phases
- **THEN** the operation raises cancellation and publishes no completed result or receipt

#### Scenario: Baseline/public-API cancellation is reported distinctly, not as a generic error
- **WHEN** a baseline or public-API command handler's `CancellationToken` is cancelled and the corresponding
  Core application service raises `OperationCanceledException`
- **THEN** the handler reports a typed `cancelled` completion (a distinct status/kind in `--format json`, a
  distinct human message) rather than folding it into that command's generic `<command> error` message

#### Scenario: Baseline/public-API cancellation racing publication does not write
- **WHEN** a baseline or public-API command handler's `CancellationToken` is cancelled after Core returns a
  successful outcome but before the handler's own write/rename step
- **THEN** the handler re-checks the token immediately before that write/rename and does not write, leaving
  any existing baseline/snapshot file at the destination unchanged
