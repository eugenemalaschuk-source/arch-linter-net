# analysis-profile/v1 phase and counter dictionary

This is the stability contract for `analysis-profile/v1` (`AnalysisProfileId.V1`, `src/ArchLinterNet.Core/Profiling/`). Issue #365 (persistent cache) and issue #408 (bounded parallel scanning) populate the reserved fields listed here with real values; issue #409 diffs post-optimization evidence against this dictionary. None of these fields ever affect finding identity, session identity, ordering, or exit status — see `openspec/specs/analysis-profile/spec.md`.

## Top-level fields

| Field | Type | Determinism | Meaning |
|---|---|---|---|
| `SchemaId` | string | deterministic | Always `"analysis-profile/v1"`. |
| `CompletionStatus` | enum | deterministic | `Success`, `ValidationFailure`, `PreparationFailure`, or `Cancelled` — the actual analysis outcome of the profiled run. A report-publication failure is represented by `Output.OutputFailed`; it does not rewrite a completed analysis as `PreparationFailure`. |
| `CancellationObserved` | bool | deterministic | `true` only when cooperative cancellation was observed during the run (see `openspec/specs/cooperative-cancellation/spec.md`). |
| `Counters` | object | deterministic | See below. |
| `Phases` | array | mixed | `Name`/`Indent`/`Ordinal`/`Count` are deterministic; `ElapsedMs` and `ProcessorTimeMs` are environment-dependent measurements, `null` when no `ValidationTiming` instance backed the run. |
| `Output` | object | host-dependent | Actual report-publication result: committed, failed, staged, and uncommitted sink counts plus `OutputFailed`. |
| `Measurements` | object or null | environment-dependent | `null` when no `ValidationTiming` instance backed the run. |

## `Counters` (deterministic)

| Field | Meaning | Source |
|---|---|---|
| `PolicyCompositions` | Number of times the policy document was composed. Always `1` for one snapshot's lifetime. | `ArchitectureAnalysisSnapshotCounters.PolicyCompositions` |
| `ProjectGraphEvaluations` | `1` ordinarily/no-restore, `2` after an `--ensure-built` post-build reload. | `ArchitectureAnalysisSnapshotCounters.ProjectGraphEvaluations` |
| `AssemblyLoads` | Target-assembly load *operations* performed while creating the snapshot (not the retained assembly count). | `ArchitectureAnalysisSnapshotCounters.AssemblyLoads` |
| `DiscoveredProjectCount` | Projects discovered for the retained snapshot after any post-build reload. | `ArchitectureAnalysisContext.ProjectDiscovery` |
| `RetainedAssemblyCount` | Successfully resolved target assemblies retained by the snapshot. | `ArchitectureAnalysisContext.TargetAssemblies` |
| `SelectedAssemblyCount` | Target assemblies selected for resolution, including selected assemblies that were missing. | `TargetAssemblies` + `MissingAssemblyNames` |
| `ModesEvaluated` | Number of distinct modes (`strict`/`audit`) evaluated so far against the snapshot. | `ArchitectureAnalysisSnapshotCounters.ModesEvaluated` |
| `SnapshotMaterializations` | Number of logical retained snapshots materialized for this profile. A successful snapshot always reports `1`; an internal post-build runner reload is not a second logical snapshot. | `ArchitectureAnalysisSnapshotCounters.SnapshotMaterializations` |
| `FactIndexMaterializations` | Number of lazy `ArchitectureSourceFileFactIndex` data builds for the retained snapshot. It is `0` when no contract accesses the index and otherwise `1`; it never exceeds `1` for a snapshot. | `ArchitectureSourceFileFactIndex.BuildData` via `ArchitectureAnalysisSnapshotCounters` |
| `SourceScanPasses` | Number of source-tree scan passes performed while materializing the fact index. It is `0` when no source-root scan is needed and otherwise `1`. | `ArchitectureSourceFileFactIndex.RunSourceScan` via `ArchitectureAnalysisSnapshotCounters` |
| `SourceFilesScanned` | Number of owned C# source files parsed by the fact-index source scan. | `ArchitectureSourceFileFactIndex.RunSourceScan` via `ArchitectureAnalysisSnapshotCounters` |
| `ContractFamilyCounts` | Map of contract-family name → number of contracts executed for that family across every evaluated mode. Repeated family phases are summed, never overwritten by the last mode. | `ValidationTiming` per-family `Count` (see `ArchitectureContractExecutor`) |
| `ContractFamilyResultCounts` | Map of contract-family name → findings/cycles (and coverage summaries) produced across every evaluated mode. | `ArchitectureContractExecutor` result inventory |
| `RenderedSinkCount` | Number of distinct normal-report formats (human/json/sarif) whose rendering actually completed, deduplicated across destinations. It is `0` when cancellation interrupts before the first completed render, even if sinks were configured. | CLI: `ReportCoordinator` routing evidence. Testing API: always `0` (no CLI-style sinks exist for a direct `ArchitectureValidationBuilder` call). |
| `OutputSinkCount` | Number of configured output destinations (stdout/stderr/file). | Same as above. |
| `Cache` | Issue #365's persistent `analysis-cache/v1`. `Status` is `NotApplicable` (all fields `0`, `Mode` `"disabled"`) unless a run configured `--cache`/`WithCache()` with anything other than disabled, in which case `Status` is `Active`. `Lookups`/`Hits`/`Misses` come from real pre-run reuse checks. `Rejects`, `Writes`, `BytesRead`/`BytesWritten`, `IneligibleUnitCount`, `CorruptionEvents`, and `CancelledBeforePublish` reflect real lookup and population activity. `Mode` is `"disabled"`/`"auto"`/`"path"` (never the resolved absolute cache location). `RejectReasonCounts` maps only reject outcomes, never a normal `Missing` miss, so its values always sum to `Rejects`. | `AnalysisProfileCacheCounters` |
| `Concurrency` | Issue #408's bounded parallel scanning. `Status` is `NotApplicable` (every numeric field `0`, including `MaxParallelism`) unless at least one scanning phase (type loading, source-file fact-index materialization) actually took the bounded-parallel code path for this run, in which case `Status` is `Active` and `MaxParallelism` reports the resolved effective degree (`--max-parallelism`/`WithMaxParallelism()`, defaulting to `max(1, min(Environment.ProcessorCount, 4))`) that was in effect for that run. `ScheduledWorkItems`/`CompletedWorkItems` count partition units (one per target assembly for type loading; one per assembly or source root for fact-index materialization). `ObservedMaxConcurrency` is the highest number of partition workers observed running concurrently. `MergeOperations` counts deterministic merge steps (one per phase that ran in parallel). | `AnalysisProfileConcurrencyCounters` |

The `Phases` array may also contain `selector_predicate_evaluation` when compiled CEL
selectors were evaluated. Its deterministic `Count` is the runtime number of predicate
invocations recorded by the analysis session; it is not a generated workload estimate. When
timing is enabled, its `ElapsedMs` is a high-resolution aggregate wall-time measurement for the
predicate evaluations. `ProcessorTimeMs` is intentionally `null`: process-level CPU time cannot
be attributed to individual predicate evaluations. When timing is not enabled, both fields are
`null`, never synthetic zero values.

## `Output` (actual publication)

`CommittedSinkCount` includes committed file sinks and successfully delivered stream sinks.
`StagedSinkCount` records file sinks that passed staging, including ones later committed.
`FailedSinkCount` and `UncommittedSinkCount` preserve the routing result when publication fails or is cancelled.
`OutputFailed` is true for partial or total output failure; it is false for fully committed and cancellation-only routing outcomes.
When `OutputFailed` is true after analysis completed, `CompletionStatus` still describes that completed analysis (`Success` or `ValidationFailure`); the CLI's runtime-error exit is described by `Output`, not misclassified as a preparation failure.

## Phase names

| Phase | Indent | Meaning |
|---|---|---|
| `total` | 0 | Whole single-mode `Validate` call, or the `total` wrapper `ExecuteCombinedModes` measures around snapshot construction. |
| `policy_composition` | 0 | Policy load, import resolution, baseline merge, severity validation, contract-ID selection. |
| `yaml_loading` | 1 | Sub-phase of `load_and_setup`: policy YAML parse. |
| `baseline_loading` | 1 | Sub-phase of `load_and_setup`: baseline file merge, when configured. |
| `load_and_setup` | 0 | Project discovery, assembly resolution, session construction. |
| `root_resolution` | 1 | Sub-phase of `load_and_setup`/`post_ensure_built_reload`: repository root resolution. |
| `condition_set_resolution` | 1 | Sub-phase: named condition-set lookup for conditional compilation symbols. |
| `assembly_resolution` | 1 | Sub-phase: target-assembly discovery/load. |
| `build_state_preflight` | 0 | Build-state preflight — includes an actual `dotnet build` invocation under `--ensure-built` when the build state is stale; a fast up-to-date check otherwise. This is the "restore/build/preparation time" the benchmark harness (see below) separates from analysis time. |
| `post_ensure_built_reload` | 0 | Second project-discovery/assembly-resolution pass after a successful `--ensure-built` build. |
| `configuration_check` | 0 | Contract-checker phase: `analysis` configuration validation. |
| `policy_consistency_check` | 0 | Contract-checker phase: cross-contract policy consistency checks. |
| `contract_checks` | 0 | Wraps every per-family phase below. |
| `<family name>` (e.g. `dependency`, `layer`, `cycle`, `coverage`, ...) | 1 | One phase per contract family in `ArchitectureContractCatalog.FamiliesInOrder`, each carrying a deterministic `Count` of contracts executed for that family. |
| `post_processing` | 0 | Unmatched-ignore resolution and related post-processing. |
| `render_human` | 0 | Render a normal human report document after the analysis outcome is known. Recorded only once that rendering completes. |
| `render_json` | 0 | Render a normal JSON report document after the analysis outcome is known. Recorded only once that rendering completes. |
| `render_sarif` | 0 | Render a normal SARIF report document after the analysis outcome is known. Recorded only once that rendering completes. |
| `output_staging` | 0 | Stage normal file report sinks before any file commit. |
| `output_stream_write` | 0 | Write normal report content to stdout/stderr destinations. |
| `output_commit` | 0 | Commit successfully staged normal file report sinks by rename. |

Every ordinary phase also records `ProcessorTimeMs`, the process CPU-time delta measured during that
phase. It is an environment-dependent measurement and can overlap for nested phases. The
`selector_predicate_evaluation` phase is the exception: its `ProcessorTimeMs` is always `null`
because process CPU time cannot be attributed to an individual predicate evaluation.

## Issue #493 preparation-reuse attribution

The #493 evidence contract composes `benchmark-evidence/v1` around one or more
raw `analysis-profile/v1` documents. The profile remains the inner
ArchLinterNet boundary; the evidence envelope adds command, process, projection,
revision, preparation-boundary, cache, and resource attribution. These labels
must be retained for every measured child process or in-process projection so a
batch cannot hide which work was repeated.

### Command and projection identity

The full-governance workload names the command families it actually exercises:

| Required or optional family | Attribution rule |
|---|---|
| Strict validation | Candidate projection; retain its process/projection identity and exit/publication result. |
| Audit validation | Candidate projection; it may share an immutable snapshot only in the one-process comparison. |
| No-new-debt | Candidate projection; do not infer it from strict or audit unless it was invoked. |
| Architecture Health | Candidate projection; retain its own command identity even when facts are shared. |
| Current-side change snapshot | Candidate projection; record the current-side revision role. |
| Topology, measure, baseline/reference, public API | Optional projections; include them only when the workflow manifest declares and measures them. |

An independent one-shot process owns one snapshot and its preparation counters.
An in-process projection records the immutable snapshot/session it used and
whether its facts were shared or remained process-bound. A projection descriptor
is evidence of what ran; it is not permission to claim that an unsupported
command was served by a shared snapshot.

### Preparation and revision boundaries

`real_ms_build` and `staged_assemblies` are distinct candidate preparation
boundaries. The former includes fixture-owned restore/build work and its
receipt-backed build state; the latter begins analysis from externally built,
exactly verified staged assemblies and receipts. In both cases, the evidence
must distinguish build/setup, `build_state_preflight`,
`post_ensure_built_reload`, `load_and_setup`, assembly/fact materialization,
contract evaluation, and output publication. A staged external build is not
silently presented as analyzer preparation.

Candidate and base/reference runs have separate revision roles. Base/reference
profiles are useful for change comparisons, but their policy, project, assembly,
fact, and contract counters are not candidate reusable work. Exclude them from
candidate savings, prepared-state break-even, and whole-workflow candidate
upper-bound calculations.

### Cache versus prepared state

The `run.cache_mode` and `run.prepared_state_mode` values describe independent
axes. Record cache-disabled, cache-miss, and exact-request cache-hit behavior
separately from unprepared, one-process-shared, and expected persisted-prepared
behavior. `Cache.Hits` and cache-avoidable preparation are attributed to
`analysis-cache/v1`; an exact-request cache hit cannot be counted as a benefit of
persisted prepared analysis. A prepared-state value in this evidence is a model
or measured comparison label only; it does not imply a shipped store or
authorization protocol.

### Expected effect and break-even

Before any prepared-analysis implementation, record the deterministic candidate
preparation/fact work, the cold prepare cost `C`, the representative independent
consumer/process count and mix `R`, and the per-consumer load/authorization cost
`L`. If one independent consumer repeats preparation cost `P`, compare:

```text
independent one-shot work = R × P
prepared-state model       = C + R × L
```

When `P > L`, the first candidate crossover is the smallest representative
`R` for which `R × P` exceeds `C + R × L`; otherwise record that no crossover
was observed. Report small, medium, and large expected effects, the whole-
workflow upper bound, storage/I/O/allocation/memory trade-offs, success and kill
criteria, and uncertainty. An outcome A decision requires this evidence and a
measured effect beyond the one-process alternative. Outcomes B and C must state
whether the opportunity is deferred, not reproduced, or routed to another
benchmark/implementation owner.

The explicit #493 matrix is manual and hardware-sensitive. Refresh it through
the issue-specific benchmark harness entry point once implementation lands;
the harness must write the checked-in synthetic/anonymized evidence artifact
only on explicit invocation. Normal tests validate deterministic serialization,
field attribution, canonical equivalence, and the effect model; they do not run
the multi-process decision matrix or rewrite measurements.

## Deterministic consumer-shaped regression evidence (issue #654)

[`RepeatedWorkRegressionEvidenceTests`](../../tests/ArchLinterNet.Core.Tests/RepeatedWorkRegressionEvidenceTests.cs)
is the focused Core fixture for issue #654. It is synthetic and anonymized: 24
discovered projects, 16 repeated metadata-family contracts, and two public-API
contracts against one already-loaded test assembly. Each covered family runs in
its own fresh session and must transition its own counter from `0` before the
first contract to `1` after it, then remain at `1` through the rest of the
fan-out. This prevents one family from seeding a shared counter and masking a
bypass in another. A literal count and SHA-256 checksum lock a non-empty,
ordered canonical projection; a temporary policy also asserts actual Testing
API strict/audit outcomes and CLI exit codes. These internal counters and
goldens are the normative regression evidence for the consumer shape; they
complement `analysis-profile/v1` and do not extend or alter its versioned
schema. No `analysis-profile/v1` field exposes these internal counters.

### Canonical golden provenance

The canonical constants were independently measured before either optimization,
from detached baseline commit `ef78023f420a6b2670b0c4fc6ad426df799c0dc4`.
That commit is the direct parent of #653 (`c00c49cd`) and therefore also
precedes #652 (`040779f2`). A temporary probe reproduced this fixture's
contracts, 24 project facts, test-assembly selector, execution modes, and
`ArchitectureFindingMapper`, while compiling and running only baseline source.
It passed with the following output:

| Revision | Strict projection | Audit projection |
|---|---|---|
| `ef78023f` baseline | `48` / `925FD7BAB41B0F638A3C0ED73C3D09E50018FC1AB70E8C539E39FB8207581849` | `3` / `9259819F5A173F5B054D99D3A0F7334DEF3154F1E30B5E954D0B46E688C161BE` |
| Current #654 fixture | `48` / `925FD7BAB41B0F638A3C0ED73C3D09E50018FC1AB70E8C539E39FB8207581849` | `3` / `9259819F5A173F5B054D99D3A0F7334DEF3154F1E30B5E954D0B46E688C161BE` |

Thus the golden is a pre-optimization oracle, not a value derived from a
second optimized execution. The current test keeps that baseline projection
locked against future changes to finding count, order, kind, or canonical
identity.

The fixture intentionally has no wall-clock or allocation thresholds. Timing and
allocation observations are hardware-sensitive and are not a release contract.
It is separate from the manually run `analysis-profile/v1` benchmark harnesses
and from the broad large-solution benchmark program reserved for issue #502: it
adds no benchmark scenarios, timing loops, generated artifacts, or performance
baselines.

## Consumer-shaped attribution evidence (issue #461)

[`ConsumerAttributionAnalysisProfileBenchmarkHarness`](../../tests/ArchLinterNet.Core.Tests/AnalysisProfile/ConsumerAttributionAnalysisProfileBenchmarkHarness.cs)
is the manual attribution harness for issue #461. It reuses the synthetic
`large-multi-host` fixture, builds it once, and then launches independent strict
CLI processes against that unchanged build state. The matrix varies only the
number of processes (`R = 1, 2, 4`) and whether the caller dispatches them
sequentially or together. Each process retains its complete `analysis-profile/v1`
document; each batch also records outer wall time, the summed inner command and
phase times, process-envelope time, deterministic counter totals, and a
canonical-result SHA-256 digest.

The boundary is deliberate:

- `analysis-profile/v1` phases and counters are the ArchLinterNet inner boundary;
- the harness stopwatch minus the profile command total is the local process
  envelope, including CLI process startup and redirected-pipe overhead;
- container, scheduler, restore-service, and CI-runner time remain caller-side
  measurements and must not be inferred from this artifact.

The checked-in [machine-readable evidence](consumer-attribution-analysis-profile-results.json)
is synthetic/anonymized and records outcome **B** for the current measurement:
inner ArchLinterNet work is measurable and repeated per independent process, but
this run does not prove a version regression. It feeds the process-count shape
and per-process counters to #502, while cross-process prepared reuse remains
owned by #492/#493. It does not create a competing benchmark framework or assert
selector/layer, real-MSBuild cache-eligibility, or changed-project advisory
findings.

The current artifact's conclusion reports `build_state_preflight` separately:
it is the dominant phase in the 1-process sample and remains roughly 82% of
inner time across the matrix. The harness launches the Release CLI DLL and
verifies that its SHA-256 equals the `tools/net10.0/any/ArchLinterNet.Cli.dll`
entry inside the selected package before recording package provenance. It
records the launched file version/DLL SHA-256, package id/version/file hash,
and verified package-entry assembly hash. Canonical-result equivalence includes
the current JSON contract's `cycle_diagnostics`, `coverage_summary`,
`preflight_diagnostics`, and `source_set_expansion` sections. NUnit cooperative
cancellation is passed into fixture build and each child-process wait; the
linked timeout kills the entire process tree and uses bounded cleanup.

## Benchmark scenario IDs (see `docs/internal/analysis-profile-pre-optimization-baseline.md`)

| Scenario ID | Measures |
|---|---|
| `1-cold-process-warm-filesystem-strict` | First `--ensure-built` run on a never-built fixture copy (real `dotnet build` cost included in `build_state_preflight`). |
| `2-immediate-warm-strict-repeat` | Same process series, subsequent `--ensure-built` runs (fast up-to-date check; no persistent cache exists yet). |
| `3-strict-and-audit-separate-processes` | Sum of one `--mode strict` process + one `--mode audit` process (legacy, pre-#363 style). |
| `4-combined-strict-audit-one-snapshot` | One process, `--mode strict,audit` (one #363 snapshot serving both modes). |
| `5a-one-report-sink` / `5b-three-report-sinks` | `--report json=stdout` alone vs. `--report human/json/sarif=stdout` together (proves #364's "one analysis, N sinks" invariant end to end). |
| `7b-validation-failure-completion-path` | Same fixture, a policy variant with a guaranteed contract violation. |
| `7c-preparation-failure-completion-path` | Never-built fixture copy, `--no-restore`, no receipts — build-state preflight blocks. |

The #461 attribution scenarios are documented in
[`consumer-attribution-analysis-profile-evidence.md`](consumer-attribution-analysis-profile-evidence.md)
and use IDs `1-process-sequential`, `2-process-sequential`,
`4-process-sequential`, `2-process-bounded-parallel`, and
`4-process-bounded-parallel`. They are manual attribution evidence, not normal
acceptance thresholds.

Scenario 6 ("sequential execution before #408") is not a separate timed variant — every scenario above already runs sequentially, since no parallel-scanning capability exists yet. Scenario 7's "success" path is already demonstrated by scenarios 1–5.

## Post-optimization evidence (issue #409)

`PostOptimizationAnalysisProfileBenchmarkHarness.RunPostOptimizationMatrix`
produces `docs/internal/analysis-profile-post-optimization-results.json` and
`docs/internal/analysis-profile-post-optimization-evidence.md`. It adds cache
first-population/warm-hit and sequential/default-parallel scenarios while
retaining the strict/audit and sink comparisons. The harness is Explicit/manual
because its timing is hardware-sensitive; each applicable row contains ten
valid samples and profile counters remain the correctness gate.
