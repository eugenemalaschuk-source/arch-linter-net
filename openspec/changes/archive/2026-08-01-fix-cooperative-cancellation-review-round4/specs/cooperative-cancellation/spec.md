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
  import chain, whether while a document's own subtree is being visited or between two sibling imports of
  the same parent document
- **THEN** the next import — nested or sibling — is not resolved, read, or parsed

#### Scenario: Cancellation during deep type/role/IL/source scanning
- **WHEN** the token is cancelled while `ArchitectureTypeIndex.LoadAllTypes()`, `ArchitectureRoleIndex.BuildData()`,
  `ArchitectureTypeScanner` type discovery, `ArchitectureIlMethodBodyScanner.FindMethodBodyViolations`,
  `ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations`, or `ArchitectureSourceScanner.FindMethodBodyViolations`
  is scanning a large target-assembly, source-type, or source-file set
- **THEN** scanning stops at the next assembly/type/file boundary and raises `OperationCanceledException` — for
  `ArchitectureSourceScanner` specifically, before the next source file is read during discovery, before the
  next syntax tree is analyzed after the Roslyn compilation is built, and before that compilation is built at
  all if cancellation was already observed

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

#### Scenario: Cancellation mid-render stops before the next section, mode, or finding
- **WHEN** cancellation is observed while `ReportCoordinator` is rendering one human report section (e.g.
  between violations and cycles), one mode of a combined strict+audit JSON/SARIF document, or one finding
  within a single large violations/coverage-findings list inside one section's own render call
- **THEN** rendering stops at that boundary — including mid-list, inside `FormatViolationsForHumans`,
  `FormatCoverageForHumans`, `FormatResultForCiArtifacts`, or `FormatResultAsSarif` — and
  `OperationCanceledException` propagates instead of a partial document reaching any sink; this covers
  both the per-item serialization step and the underlying `ArchitectureFindingMapper.FromViolations`
  mapping pass itself, which is checked per violation as diagnostics and finding identities are built,
  not only when the mapped result is later iterated for output

#### Scenario: A cancellation notice never overwrites an existing configured report file
- **WHEN** the CLI's own `CancellationToken` is cancelled (e.g. a real Ctrl+C) and a `--report <format>=<file>`
  file sink is configured
- **THEN** the typed cancelled completion is written to a safe stream fallback (stderr, or a configured
  stream sink) and the configured file sink is left untouched — it may hold a legitimate report from an
  earlier run of the same command

#### Scenario: A killed build/restore process that will not confirm exit is reported, not silently leaked
- **WHEN** `BuildStatePreparationService` kills a cancelled child `dotnet build`/`dotnet restore` process and
  it does not report exit within the bounded post-kill deadline
- **THEN** the operation raises a typed cleanup-timeout exception (a subtype of `OperationCanceledException`)
  identifying the process, and the CLI's cancelled completion output surfaces that process ID and deadline —
  it is not caught by the generic `OperationCanceledException` handling and reported as a bare "cancelled"
  message that discards the evidence
