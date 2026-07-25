# analysis-snapshot Specification

## Purpose
Let one composed policy, one project-graph evaluation, and one assembly load serve every requested `strict`/`audit` validation view (coverage included, via the existing `strict_coverage`/`audit_coverage` families) for one session, instead of every mode independently repeating that setup — while keeping ordinary single-mode validation exactly as simple and behaviorally unchanged as it was before this capability existed.
## Requirements
### Requirement: Snapshot composes policy once and evaluates the project graph as few times as build state requires
The system SHALL provide `ArchitectureAnalysisSnapshot`, constructed via `IArchitectureValidationApplicationService.CreateSnapshot(AnalysisSnapshotRequest, ValidationTiming?)`, which composes the effective policy exactly once for the snapshot's lifetime and runs build-state preflight exactly once. Ordinary and no-restore preparation evaluate the selected project graph and resolve/load target assemblies exactly once. Explicit `--ensure-built` preparation SHALL evaluate the project graph and resolve/load target assemblies a second time after a successful build, loading every target from its exact verified post-build output path in an isolated scope; it SHALL NOT reuse a same-simple-name assembly already loaded in the process or choose a target through environment/policy probing precedence. The policy document composed at the start of `CreateSnapshot` SHALL be reused for that second pass rather than recomposed.

#### Scenario: Creating a snapshot performs setup once for ordinary preparation
- **WHEN** `CreateSnapshot` is called for a policy and selected projects without `--ensure-built`
- **THEN** policy composition, project discovery, and assembly resolution/load each execute exactly once, producing one `ArchitectureRunnerSetup` retained by the snapshot

#### Scenario: Ensure-built reuses the composed policy across its second pass
- **WHEN** `CreateSnapshot` is called with `--ensure-built` preparation and the build succeeds
- **THEN** policy composition (policy load, baseline merge, severity validation, contract-ID selection) executes exactly once, while project discovery and assembly resolution/load execute a second time from an isolated post-build loading scope that contains the newly built artifacts

### Requirement: One snapshot serves multiple mode evaluations
The system SHALL let `ArchitectureAnalysisSnapshot.Evaluate(string mode, ValidationTiming?)` be called for `strict` and/or `audit` against the same snapshot, executing contract checks (including the `strict_coverage`/`audit_coverage` families) against the snapshot's one composed session without re-running policy composition, project discovery, or assembly resolution.

#### Scenario: Strict and audit evaluated from one snapshot
- **WHEN** a caller calls `Evaluate("strict")` followed by `Evaluate("audit")` on the same snapshot
- **THEN** both calls read from the same `ArchitectureAnalysisSession`, and no new session, project discovery, or assembly load occurs between the two calls

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
- **THEN** the command performs one policy composition, project discovery, and assembly load, evaluates both modes against the resulting snapshot, and reports both outcomes

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
The system SHALL expose `ArchitectureAnalysisSnapshotCounters` from `ArchitectureAnalysisSnapshot.Counters`, recording the actual number of policy compositions, project-graph evaluations, and assembly loads performed for the snapshot, and the number of distinct modes evaluated so far. `AssemblyLoads` SHALL count only target-assembly load operations performed while creating the snapshot, not the number of assemblies retained in its final context; a target already loaded before snapshot creation contributes zero. `PolicyCompositions` SHALL always equal `1` (the policy document is never recomposed within one snapshot's lifetime, even across an `--ensure-built` reload). `ProjectGraphEvaluations` SHALL equal `1` for ordinary/no-restore preparation and SHALL equal `2` when `--ensure-built` preparation triggers a post-build reload — it SHALL NOT be hardcoded independently of how many passes actually ran.

#### Scenario: Counters reflect one composition and multiple mode evaluations
- **WHEN** a snapshot created without `--ensure-built` has `Evaluate("strict")` and `Evaluate("audit")` both called
- **THEN** `Counters.PolicyCompositions` and `Counters.ProjectGraphEvaluations` each equal `1`, and `Counters.ModesEvaluated` equals `2`

#### Scenario: Counters reflect the ensure-built reload
- **WHEN** a snapshot is created with `--ensure-built` preparation and the build succeeds, triggering a post-build reload
- **THEN** `Counters.PolicyCompositions` equals `1` and `Counters.ProjectGraphEvaluations` equals `2`

#### Scenario: Counters exclude assemblies that were already loaded
- **WHEN** a target assembly was already loaded before snapshot creation and no target-assembly load operation occurs during setup
- **THEN** `Counters.AssemblyLoads` equals `0` even though the snapshot retains that target assembly
