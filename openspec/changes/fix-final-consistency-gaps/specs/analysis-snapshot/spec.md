## MODIFIED Requirements

### Requirement: Snapshot composes policy once and evaluates the project graph as few times as build state requires
The system SHALL provide `ArchitectureAnalysisSnapshot`, constructed via `IArchitectureValidationApplicationService.CreateSnapshot(AnalysisSnapshotRequest, ValidationTiming?)`, which composes the effective policy exactly once for the snapshot's lifetime and runs build-state preflight exactly once. Ordinary and no-restore preparation SHALL evaluate the selected project graph and create one immutable metadata-only preparation plan containing the selected verified artifact paths and identity evidence; it SHALL NOT load target assemblies into a CLR context. Explicit `--ensure-built` preparation SHALL evaluate the project graph a second time after a successful build and replace the plan with one for the exact verified post-build output paths; it SHALL NOT choose a target through environment/policy probing precedence. The policy document composed at the start of `CreateSnapshot` SHALL be reused for that second pass rather than recomposed. `Evaluate` SHALL materialize a runner from the plan only after its cache lookup misses.

#### Scenario: Creating a snapshot performs metadata-only setup once for ordinary preparation
- **WHEN** `CreateSnapshot` is called for a policy and selected projects without `--ensure-built`
- **THEN** policy composition, project discovery, and metadata-only artifact planning each execute exactly once, producing one immutable preparation plan retained by the snapshot without CLR assembly loading

#### Scenario: Ensure-built reuses the composed policy across its second planning pass
- **WHEN** `CreateSnapshot` is called with `--ensure-built` preparation and the build succeeds
- **THEN** policy composition (policy load, baseline merge, severity validation, contract-ID selection) executes exactly once, while project discovery and metadata-only artifact planning execute a second time for the exact verified post-build outputs

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

#### Scenario: Cache hits avoid assembly loads
- **WHEN** every evaluated mode is served by a verified cache hit and no runner is materialized
- **THEN** `Counters.AssemblyLoads` equals `0` even though the snapshot retains a verified metadata-only artifact plan
