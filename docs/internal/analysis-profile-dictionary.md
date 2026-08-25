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

Every phase also records `ProcessorTimeMs`, the process CPU-time delta measured during that phase. It is an environment-dependent measurement and can overlap for nested phases.

## Deterministic consumer-shaped regression evidence (issue #654)

[`RepeatedWorkRegressionEvidenceTests`](../../tests/ArchLinterNet.Core.Tests/RepeatedWorkRegressionEvidenceTests.cs)
is the focused Core fixture for issue #654. It is synthetic and anonymized: one
in-memory session represents 24 discovered projects, 16 repeated metadata-family
contract checks, and two public-API checks against one already-loaded test
assembly. The test asserts one project-metadata index, one assembly-name index,
and one exported public-API surface materialization, together with the ordered
canonical finding projection and strict/audit pass/fail outcomes. These internal
session materialization counters and projections are the normative regression
evidence for the consumer shape; they complement `analysis-profile/v1` and do
not extend or alter its versioned schema. No `analysis-profile/v1` field exposes
these internal counters.

The fixture intentionally has no wall-clock or allocation thresholds. Timing and
allocation observations are hardware-sensitive and are not a release contract.
It is separate from the manually run `analysis-profile/v1` benchmark harnesses
and from the broad large-solution benchmark program reserved for issue #502: it
adds no benchmark scenarios, timing loops, generated artifacts, or performance
baselines.

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

Scenario 6 ("sequential execution before #408") is not a separate timed variant — every scenario above already runs sequentially, since no parallel-scanning capability exists yet. Scenario 7's "success" path is already demonstrated by scenarios 1–5.

## Post-optimization evidence (issue #409)

`PostOptimizationAnalysisProfileBenchmarkHarness.RunPostOptimizationMatrix`
produces `docs/internal/analysis-profile-post-optimization-results.json` and
`docs/internal/analysis-profile-post-optimization-evidence.md`. It adds cache
first-population/warm-hit and sequential/default-parallel scenarios while
retaining the strict/audit and sink comparisons. The harness is Explicit/manual
because its timing is hardware-sensitive; each applicable row contains ten
valid samples and profile counters remain the correctness gate.
