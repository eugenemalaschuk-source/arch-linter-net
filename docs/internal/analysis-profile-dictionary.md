# analysis-profile/v1 phase and counter dictionary

This is the stability contract for `analysis-profile/v1` (`AnalysisProfileId.V1`, `src/ArchLinterNet.Core/Profiling/`). Issue #365 (persistent cache) and issue #408 (bounded parallel scanning) populate the reserved fields listed here with real values; issue #409 diffs post-optimization evidence against this dictionary. None of these fields ever affect finding identity, session identity, ordering, or exit status — see `openspec/specs/analysis-profile/spec.md`.

## Top-level fields

| Field | Type | Determinism | Meaning |
|---|---|---|---|
| `SchemaId` | string | deterministic | Always `"analysis-profile/v1"`. |
| `CompletionStatus` | enum | deterministic | `Success`, `ValidationFailure`, `PreparationFailure`, or `Cancelled` — the actual outcome of the profiled run. |
| `CancellationObserved` | bool | deterministic | `true` only when cooperative cancellation was observed during the run (see `openspec/specs/cooperative-cancellation/spec.md`). |
| `Counters` | object | deterministic | See below. |
| `Phases` | array | mixed | `Name`/`Indent`/`Ordinal`/`Count` are deterministic; `ElapsedMs` is an environment-dependent measurement, `null` when no `ValidationTiming` instance backed the run. |
| `Measurements` | object or null | environment-dependent | `null` when no `ValidationTiming` instance backed the run. |

## `Counters` (deterministic)

| Field | Meaning | Source |
|---|---|---|
| `PolicyCompositions` | Number of times the policy document was composed. Always `1` for one snapshot's lifetime. | `ArchitectureAnalysisSnapshotCounters.PolicyCompositions` |
| `ProjectGraphEvaluations` | `1` ordinarily/no-restore, `2` after an `--ensure-built` post-build reload. | `ArchitectureAnalysisSnapshotCounters.ProjectGraphEvaluations` |
| `AssemblyLoads` | Target-assembly load *operations* performed while creating the snapshot (not the retained assembly count). | `ArchitectureAnalysisSnapshotCounters.AssemblyLoads` |
| `ModesEvaluated` | Number of distinct modes (`strict`/`audit`) evaluated so far against the snapshot. | `ArchitectureAnalysisSnapshotCounters.ModesEvaluated` |
| `ContractFamilyCounts` | Map of contract-family name → number of contracts executed for that family, for whichever mode(s) were evaluated. | `ValidationTiming` per-family `Count` (see `ArchitectureContractExecutor`) |
| `RenderedSinkCount` | Number of distinct output formats rendered (human/json/sarif), deduplicated across destinations. | CLI: `ValidateCommandHandler.Profile.cs`. Testing API: always `0` (no CLI-style sinks exist for a direct `ArchitectureValidationBuilder` call). |
| `OutputSinkCount` | Number of configured output destinations (stdout/stderr/file). | Same as above. |
| `Cache` | Reserved for issue #365. `Status` is `NotApplicable`, `Lookups`/`Hits` are `0` until then. | `AnalysisProfileCacheCounters` |
| `Concurrency` | Reserved for issue #408. `Status` is `NotApplicable`, `Workers` is `0` until then. | `AnalysisProfileConcurrencyCounters` |

## Phase names (from `ValidationTiming`, unchanged by this capability)

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
