# analysis-snapshot Specification

## Purpose
TBD - created by archiving change add-analysis-snapshot. Update Purpose after archive.
## Requirements
### Requirement: Snapshot composes policy, project graph, and assemblies once
The system SHALL provide `ArchitectureAnalysisSnapshot`, constructed via `IArchitectureValidationApplicationService.CreateSnapshot(AnalysisSnapshotRequest, ValidationTiming?)`, which composes the effective policy, evaluates the selected project graph, resolves/loads target assemblies, and runs build-state preflight exactly once for the snapshot's lifetime.

#### Scenario: Creating a snapshot performs setup once
- **WHEN** `CreateSnapshot` is called for a policy and selected projects
- **THEN** policy composition, project discovery, and assembly resolution/load each execute exactly once, producing one `ArchitectureRunnerSetup` retained by the snapshot

### Requirement: One snapshot serves multiple mode evaluations
The system SHALL let `ArchitectureAnalysisSnapshot.Evaluate(string mode, ValidationTiming?)` be called for `strict` and/or `audit` against the same snapshot, executing contract checks (including the `strict_coverage`/`audit_coverage` families) against the snapshot's one composed session without re-running policy composition, project discovery, or assembly resolution.

#### Scenario: Strict and audit evaluated from one snapshot
- **WHEN** a caller calls `Evaluate("strict")` followed by `Evaluate("audit")` on the same snapshot
- **THEN** both calls read from the same `ArchitectureAnalysisSession`, and no new session, project discovery, or assembly load occurs between the two calls

#### Scenario: Combined execution matches separate runs
- **WHEN** `Evaluate("strict")` and `Evaluate("audit")` are called on one snapshot for a policy and target assemblies
- **THEN** each mode's `ValidationOutcome` (violations, cycles, coverage findings, unmatched-ignored findings, policy-consistency findings, classification facts) is identical to the `ValidationOutcome` produced by calling the existing single-mode `Validate` independently for that mode against the same inputs

### Requirement: Repeated evaluation of the same mode is memoized
The system SHALL cache the `ValidationOutcome` produced by `Evaluate(mode)` per mode for the snapshot's lifetime, so a repeated call with the same mode returns the cached outcome without re-executing contract checks.

#### Scenario: Second call for the same mode reuses the cached outcome
- **WHEN** `Evaluate("strict")` is called twice on the same undisposed snapshot
- **THEN** contract execution for `strict` runs once, and both calls return the same `ValidationOutcome` instance

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

### Requirement: Snapshot disposal is explicit and terminal
The system SHALL implement `IDisposable` on `ArchitectureAnalysisSnapshot`. After `Dispose()` is called, any subsequent `Evaluate` call SHALL throw `ObjectDisposedException`, and the snapshot SHALL NOT be reused.

#### Scenario: Evaluating a disposed snapshot throws
- **WHEN** `Evaluate(mode)` is called on a snapshot after `Dispose()` has been called
- **THEN** the call throws `ObjectDisposedException` instead of returning a result

### Requirement: Testing API exposes an explicitly owned shared snapshot
The system SHALL let `ArchLinterNet.Testing` consumers explicitly obtain one owned `ArchitectureAnalysisSnapshot`-backed object from `ArchitectureValidationBuilder` and evaluate `strict`/`audit` against it, while `ArchitectureValidationBuilder.ValidateStrict()`/`ValidateAudit()` continue to perform independent, non-shared runs as before this change.

#### Scenario: Multiple assertions share one owned snapshot
- **WHEN** a test explicitly creates a shared snapshot from the builder and evaluates strict and audit against it inside a `using` block
- **THEN** both evaluations reuse the one composed snapshot, and disposal happens deterministically at the end of the `using` block

#### Scenario: Existing builder usage is unaffected
- **WHEN** a test calls `ArchitectureValidationBuilder.ValidateStrict()` or `ValidateAudit()` directly, without requesting a shared snapshot
- **THEN** behavior and results are identical to before this change

### Requirement: CLI validate command evaluates a comma-separated mode list from one snapshot
The system SHALL let the CLI `validate` command's `--mode` option accept a comma-separated list of `strict`/`audit` values. For more than one requested mode, the command SHALL build exactly one `ArchitectureAnalysisSnapshot` and evaluate each requested mode against it, reporting each mode's outcome and failing the command if any requested mode's outcome fails. A single mode value SHALL behave exactly as before this change.

#### Scenario: Requesting strict and audit together builds one snapshot
- **WHEN** the CLI `validate` command runs with `--mode strict,audit`
- **THEN** the command performs one policy composition, project discovery, and assembly load, evaluates both modes against the resulting snapshot, and reports both outcomes

#### Scenario: Single-mode CLI invocation is unchanged
- **WHEN** the CLI `validate` command runs with `--mode strict` (a single value)
- **THEN** the command's behavior, output, and exit code are identical to before this change

### Requirement: Typed counters record composition and evaluation counts
The system SHALL expose `ArchitectureAnalysisSnapshotCounters` from `ArchitectureAnalysisSnapshot.Counters`, recording the number of policy compositions, project-graph evaluations, and assembly loads performed for the snapshot (each SHALL be exactly `1` per created snapshot), and the number of distinct modes evaluated so far.

#### Scenario: Counters reflect one composition and multiple evaluations
- **WHEN** a snapshot is created and then `Evaluate("strict")` and `Evaluate("audit")` are both called
- **THEN** `Counters.PolicyCompositions`, `Counters.ProjectGraphEvaluations`, and `Counters.AssemblyLoads` each equal `1`, and `Counters.ModesEvaluated` equals `2`

