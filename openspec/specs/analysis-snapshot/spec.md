# analysis-snapshot Specification

## Purpose
Let one composed policy and one project-graph preparation plan serve every
requested `strict`/`audit` validation view (coverage included, via the existing
`strict_coverage`/`audit_coverage` families), with zero CLR assembly loads for
cache-only outcomes and at most one lazy runner materialization shared by
cache-miss evaluations — while keeping ordinary single-mode validation exactly
as simple and behaviorally unchanged as it was before this capability existed.

## Requirements

### Requirement: Snapshot composes policy once and evaluates the project graph as few times as build state requires
The system SHALL provide `ArchitectureAnalysisSnapshot`, constructed via `IArchitectureValidationApplicationService.CreateSnapshot(AnalysisSnapshotRequest, ValidationTiming?)`, which composes the effective policy exactly once for the snapshot's lifetime and runs build-state preflight exactly once. Ordinary and no-restore preparation SHALL evaluate the selected project graph and create one immutable metadata-only preparation plan containing the selected verified artifact paths and identity evidence; it SHALL NOT load target assemblies into a CLR context. Explicit `--ensure-built` preparation SHALL evaluate the project graph a second time after a successful build and replace the plan with one for the exact verified post-build output paths; it SHALL NOT choose a target through environment/policy probing precedence. The policy document composed at the start of `CreateSnapshot` SHALL be reused for that second pass rather than recomposed. `Evaluate` SHALL materialize a runner from the plan only after its cache lookup misses.

#### Scenario: Creating a snapshot performs setup once for ordinary preparation
- **WHEN** `CreateSnapshot` is called for a policy and selected projects without `--ensure-built`
- **THEN** policy composition, project discovery, and metadata-only artifact planning each execute exactly once, producing one immutable preparation plan retained by the snapshot without CLR assembly loading

#### Scenario: Ensure-built reuses the composed policy across its second pass
- **WHEN** `CreateSnapshot` is called with `--ensure-built` preparation and the build succeeds
- **THEN** policy composition (policy load, baseline merge, severity validation, contract-ID selection) executes exactly once, while project discovery and metadata-only artifact planning execute a second time for the exact verified post-build outputs

### Requirement: One snapshot serves multiple mode evaluations
The system SHALL let `ArchitectureAnalysisSnapshot.Evaluate(string mode, ValidationTiming?)` be called for `strict` and/or `audit` against the same immutable preparation plan without re-running policy composition, project discovery, or artifact planning. Each evaluation SHALL perform its cache lookup before runner/session materialization. Cache-only outcomes SHALL not create a session; when any evaluation misses, the snapshot SHALL materialize one runner/session and reuse it for later misses without re-running policy composition, project discovery, artifact planning, or target-assembly loading.

#### Scenario: Strict and audit evaluated from one snapshot
- **WHEN** a caller calls `Evaluate("strict")` followed by `Evaluate("audit")` on the same snapshot
- **THEN** either call may return from cache without a session, and if either call misses, both miss-path evaluations use the one lazily materialized `ArchitectureAnalysisSession` without a second project discovery, artifact plan, or assembly load

#### Scenario: Combined execution matches separate runs
- **WHEN** `Evaluate("strict")` and `Evaluate("audit")` are called on one snapshot for a policy and target assemblies
- **THEN** each mode's `ValidationOutcome` (violations, cycles, coverage findings, unmatched-ignored findings, policy-consistency findings, classification facts) is identical to the `ValidationOutcome` produced by calling the existing single-mode `Validate` independently for that mode against the same inputs

### Requirement: Each mode's unmatched-ignore diagnostics are isolated from other evaluated modes
The system SHALL report only the unmatched-ignore diagnostics produced by a given `Evaluate(mode)` call's own contract checks in that call's `ValidationOutcome`, regardless of which other modes were evaluated on the same snapshot before or after it, and regardless of evaluation order.

#### Scenario: A mode with no unmatched ignores of its own does not inherit another mode's
- **WHEN** `Evaluate("strict")` is called on a snapshot whose strict contracts produce an unmatched-ignore diagnostic, followed by `Evaluate("audit")` whose audit contracts produce none of their own
- **THEN** the `audit` outcome's `UnmatchedIgnoredViolations` is empty, and the `strict` outcome's `UnmatchedIgnoredViolations` contains only the diagnostic strict's own checks produced

#### Scenario: Evaluation order does not change isolation
- **WHEN** the same two modes from the previous scenario are instead evaluated in the opposite order
- **THEN** each mode's outcome still contains only the unmatched-ignore diagnostics its own checks produced

### Requirement: Repeated evaluation of the same mode is memoized
The system SHALL cache the `ValidationOutcome` produced by `Evaluate(mode)` per mode for the snapshot's lifetime, so a repeated call with the same mode returns the cached outcome without re-executing contract checks.

#### Scenario: Second call for the same mode reuses the cached outcome
- **WHEN** `Evaluate("strict")` is called twice on the same undisposed snapshot
- **THEN** contract execution for `strict` runs once, and both calls return the same `ValidationOutcome` instance

### Requirement: Snapshot evaluation and disposal are serialized
The system SHALL serialize `Evaluate` and `Dispose` for one `ArchitectureAnalysisSnapshot`. Concurrent evaluations SHALL execute one at a time against the shared session and each outcome SHALL contain only its own mode's diagnostics; `Dispose` SHALL wait for an in-progress evaluation before releasing snapshot-owned resources.

#### Scenario: Concurrent mode evaluation does not overlap session access
- **WHEN** callers concurrently invoke `Evaluate("strict")` and `Evaluate("audit")` on one snapshot
- **THEN** the two session evaluations execute serially, the outcomes remain mode-isolated, and the counters and memoized outcomes remain internally consistent

### Requirement: Single-mode validation remains simple and unchanged
The system SHALL implement `IArchitectureValidationApplicationService.Validate(ValidationRequest, ValidationTiming?)` on top of `CreateSnapshot` and `Evaluate`, producing the same `ValidationOutcome` it produced before this change for the same request, with the snapshot disposed before `Validate` returns.

#### Scenario: Existing single-mode callers are unaffected
- **WHEN** an existing caller invokes `Validate` for a single mode as before this change
- **THEN** the returned `ValidationOutcome` is unchanged, and no new object or disposal responsibility is imposed on that caller

### Requirement: Invalid build state fails the whole snapshot
The system SHALL treat a blocked build-state preflight result as a property of the snapshot: when `CreateSnapshot`'s preflight blocks, `Evaluate(mode)` SHALL return the same blocked `ValidationOutcome` shape used today, for every requested mode, without executing contract checks for that mode.

#### Scenario: Blocked preflight blocks every requested mode
- **WHEN** build-state preflight blocks during `CreateSnapshot`
- **THEN** every subsequent `Evaluate(mode)` call on that snapshot returns a blocked `ValidationOutcome` and does not execute contract checks

### Requirement: A contract-ID filter is validated per mode at evaluation time
The system SHALL validate a `--contract-id`/`ContractIds` filter on a snapshot meant to serve any/all requested modes (a `CreateSnapshot` call, as opposed to the single-mode `Validate` path) against the union of strict and audit contract IDs when the snapshot is created, and additionally SHALL validate it again against exactly the evaluated mode's own contract IDs each time `Evaluate(mode)` is called, throwing the same "Unknown contract IDs" error an independent single-mode `Validate` call for that mode would throw for a requested ID that mode does not recognize.

#### Scenario: An ID valid in only one mode fails evaluation of the other mode
- **WHEN** a snapshot is created with a `ContractIds` filter containing an ID declared only by a strict contract
- **THEN** `CreateSnapshot` does not throw, `Evaluate("strict")` succeeds, and `Evaluate("audit")` throws the same "Unknown contract IDs" error an independent `Validate` call for `audit` with that filter would throw

#### Scenario: Combined execution matches independent per-mode contract-ID validation
- **WHEN** the same `ContractIds` filter and modes are evaluated once via a snapshot's `Evaluate` calls and once via independent single-mode `Validate` calls
- **THEN** both paths accept or reject each requested mode identically

### Requirement: Snapshot disposal is explicit and terminal
The system SHALL implement `IDisposable` on `ArchitectureAnalysisSnapshot`. After `Dispose()` is called, any subsequent `Evaluate` call SHALL throw `ObjectDisposedException`, and the snapshot SHALL NOT be reused. The public snapshot API SHALL NOT expose the mutable runner, session, or target-assembly context; disposal SHALL release the snapshot's references to those objects so a collectible post-build loading scope can be collected.

#### Scenario: Evaluating a disposed snapshot throws
- **WHEN** `Evaluate(mode)` is called on a snapshot after `Dispose()` has been called
- **THEN** the call throws `ObjectDisposedException` instead of returning a result

#### Scenario: Mutable runner state cannot bypass the snapshot lifecycle
- **WHEN** a consumer holds an `ArchitectureAnalysisSnapshot`
- **THEN** it can evaluate modes and inspect immutable snapshot facts, but cannot obtain the mutable contract runner or session directly

### Requirement: Testing API exposes an explicitly owned shared snapshot
The system SHALL let `ArchLinterNet.Testing` consumers explicitly obtain one owned `ArchitectureAnalysisSnapshot`-backed object from `ArchitectureValidationBuilder` and evaluate `strict`/`audit` against it, while `ArchitectureValidationBuilder.ValidateStrict()`/`ValidateAudit()` continue to perform independent, non-shared runs as before this change.

#### Scenario: Multiple assertions share one owned snapshot
- **WHEN** a test explicitly creates a shared snapshot from the builder and evaluates strict and audit against it inside a `using` block
- **THEN** both evaluations reuse the one composed snapshot, and disposal happens deterministically at the end of the `using` block

#### Scenario: Existing builder usage is unaffected
- **WHEN** a test calls `ArchitectureValidationBuilder.ValidateStrict()` or `ValidateAudit()` directly, without requesting a shared snapshot
- **THEN** behavior and results are identical to before this change

### Requirement: CLI validate command evaluates a comma-separated mode list from one snapshot and emits one machine-readable document
The system SHALL let the CLI `validate` command's `--mode` option accept a comma-separated list of `strict`/`audit` values. For more than one requested mode, the command SHALL build exactly one `ArchitectureAnalysisSnapshot` and evaluate each requested mode against it, failing the command if any requested mode's outcome fails. For `--format json`, the command SHALL emit exactly one JSON document containing one result per requested mode. For `--format sarif`, the command SHALL emit exactly one SARIF document whose `runs` array contains one run per requested mode. For `--format human`, the command reports each mode's section sequentially. A single mode value SHALL behave exactly as before this change, including emitting exactly the single-mode JSON/SARIF document shape used before this change.

#### Scenario: Requesting strict and audit together builds one snapshot
- **WHEN** the CLI `validate` command runs with `--mode strict,audit`
- **THEN** the command performs one policy composition and project discovery, evaluates both modes against one prepared snapshot, and materializes at most one runner only if an evaluated mode misses cache

#### Scenario: Combined JSON output is one valid document
- **WHEN** the CLI `validate` command runs with `--mode strict,audit --format json`
- **THEN** stdout parses as exactly one JSON document, containing one result entry per requested mode

#### Scenario: Combined SARIF output is one valid document
- **WHEN** the CLI `validate` command runs with `--mode strict,audit --format sarif`
- **THEN** stdout parses as exactly one SARIF document with `version` `"2.1.0"`, whose `runs` array contains one run per requested mode

#### Scenario: Single-mode CLI invocation is unchanged
- **WHEN** the CLI `validate` command runs with `--mode strict` (a single value)
- **THEN** the command's behavior, output (including the single-document JSON/SARIF shape), and exit code are identical to before this change

### Requirement: Typed counters record actual composition and evaluation counts
The system SHALL expose `ArchitectureAnalysisSnapshotCounters` from `ArchitectureAnalysisSnapshot.Counters`, recording the actual number of policy compositions, project-graph evaluations, and target-assembly load operations performed for the snapshot, and the number of distinct modes evaluated so far. `AssemblyLoads` SHALL count only target-assembly load operations performed while lazily materializing the runner after a cache miss, not assemblies retained by the metadata-only preparation plan; a snapshot served entirely by cache hits contributes zero. `PolicyCompositions` SHALL always equal `1` (the policy document is never recomposed within one snapshot's lifetime, even across an `--ensure-built` reload). `ProjectGraphEvaluations` SHALL equal `1` for ordinary/no-restore preparation and SHALL equal `2` when `--ensure-built` preparation triggers a post-build reload — it SHALL NOT be hardcoded independently of how many passes actually ran.

#### Scenario: Counters reflect one composition and multiple mode evaluations
- **WHEN** a snapshot created without `--ensure-built` has `Evaluate("strict")` and `Evaluate("audit")` both called
- **THEN** `Counters.PolicyCompositions` and `Counters.ProjectGraphEvaluations` each equal `1`, and `Counters.ModesEvaluated` equals `2`

#### Scenario: Counters reflect the ensure-built reload
- **WHEN** a snapshot is created with `--ensure-built` preparation and the build succeeds, triggering a post-build reload
- **THEN** `Counters.PolicyCompositions` equals `1` and `Counters.ProjectGraphEvaluations` equals `2`

#### Scenario: Counters exclude assemblies that were already loaded
- **WHEN** every evaluated mode is served by a verified cache hit and no runner is materialized
- **THEN** `Counters.AssemblyLoads` equals `0` even though the snapshot retains a verified metadata-only artifact plan

### Requirement: Snapshot defers runner materialization until a cache miss requires it
The system SHALL construct a snapshot from an immutable preparation plan without CLR assembly loading. Each `Evaluate(mode)` SHALL perform that mode's cache lookup before runner/session materialization; a hit SHALL return without `BuildRunnerFor` or an assembly load context. The first miss SHALL materialize exactly one runner for the snapshot, and later misses SHALL reuse that runner while a hit in one mode SHALL not prevent evaluation of another mode.

#### Scenario: Both modes hit without materialization
- **WHEN** strict and audit entries both match a prepared snapshot
- **THEN** both outcomes return from cache and the snapshot records zero assembly loads

#### Scenario: One mode hits and the other misses
- **WHEN** strict is a cache hit and audit is a cache miss
- **THEN** strict returns without setup, audit materializes one runner, and no second runner is created for later misses

### Requirement: Lazy snapshot disposal is terminal in either state
The system SHALL dispose correctly before or after runner materialization and SHALL not permit evaluation after disposal.

#### Scenario: Unmaterialized snapshot is disposed
- **WHEN** a snapshot with only cache hits is disposed
- **THEN** no assembly load scope is created and later evaluation throws `ObjectDisposedException`

### Requirement: Combined CLI modes share one ensure-built preparation

The CLI `validate` command invoked with `--mode strict,audit --ensure-built` SHALL create exactly one `ArchitectureAnalysisSnapshot` and use its one snapshot-owned build-state preparation for both requested modes. Any required post-build receipt verification SHALL remain part of that preparation; the second mode SHALL NOT initiate a second build, project-graph preparation, or snapshot. After both modes have been evaluated, the snapshot counters SHALL report one policy composition and two evaluated modes. Each result and the aggregate command exit category SHALL be equivalent to evaluating the corresponding standalone mode against the same verified build state.

#### Scenario: Combined ensure-built validation evaluates strict and audit

- **WHEN** the CLI validates one policy with `--mode strict,audit --ensure-built`
- **THEN** it performs one snapshot-owned preparation, evaluates both modes from
  that snapshot, preserves each standalone mode's result, and exits successfully
  only when both requested modes pass

### Requirement: Additional combined-mode reports consume completed outcomes

When a combined CLI validation routes human, JSON, and/or SARIF reports, every requested sink SHALL render the already-completed strict and audit outcomes from the one snapshot. Adding report sinks SHALL change only rendering and output evidence; it SHALL NOT compose policy, prepare the project graph, materialize a second analysis session, or execute either mode again.

#### Scenario: Combined validation routes multiple report formats

- **WHEN** a `--mode strict,audit` validation requests JSON and SARIF report
  sinks in addition to its normal output
- **THEN** the report artifacts contain the two completed mode results and the
  profile's analysis counters remain those of the one shared snapshot

### Requirement: Measurement cancellation is terminal for a snapshot
The snapshot SHALL apply its cancellation lifecycle uniformly to `Measure()`
and `Evaluate()`. If cancellation is observed while a measurement lazily
materializes analysis facts, the snapshot SHALL become cancelled before the
operation rethrows, and all later measurement or validation attempts SHALL be
rejected as reuse of a cancelled snapshot.

#### Scenario: A cancelled measurement cannot be followed by evaluation
- **WHEN** `Measure()` observes cancellation while materializing its analysis
  session
- **THEN** a subsequent `Measure()` or `Evaluate()` on that snapshot throws
  cancellation rather than reusing the partial session
