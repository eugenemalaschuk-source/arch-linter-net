# analysis-profile Specification

## Purpose
Give #365 (persistent cache), #408 (bounded parallel scanning), and #409 (post-optimization comparison) a stable, versioned, machine-readable `analysis-profile/v1` contract to measure against — built on top of the existing `analysis-snapshot` counters and `cli-timing` phase measurements, never affecting finding identity, session identity, ordering, or exit status.
## Requirements
### Requirement: A versioned, machine-readable analysis profile is available
The system SHALL provide an `AnalysisProfile` model identified by the constant schema id `analysis-profile/v1` (`AnalysisProfileId.V1`), buildable from an existing `ValidationTiming?`, `ArchitectureAnalysisSnapshotCounters`, sink render/output counts, and a completion status, without modifying `ValidationTiming`'s existing human-readable report shape or `ArchitectureAnalysisSnapshotCounters`'s existing fields.

#### Scenario: Building a profile does not change existing timing/counter behavior
- **WHEN** an `AnalysisProfile` is built from a `ValidationTiming` instance that also has `WriteReport` called on it
- **THEN** the human-readable timing report's phase names, order, and values are identical to what they would be without building a profile

### Requirement: Deterministic counters are separated from environment-dependent measurements
The system SHALL group `AnalysisProfile` fields into a `Counters` section (policy compositions, project-graph evaluations, assembly loads, actual discovered-project/retained-assembly/selected-assembly inventories, modes evaluated, snapshot materializations, fact-index materializations, source-scan passes, source files scanned, contract-family execution/result counts, rendered-sink count, output-sink count), an `Output` section (actual committed/failed/staged/uncommitted sink counts and output-failure state), and a nullable `Measurements` section (peak working set bytes, allocated bytes) plus nullable per-phase `ElapsedMs` and `ProcessorTimeMs`. `Counters.PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, inventory counts, `ModesEvaluated`, `SnapshotMaterializations`, `FactIndexMaterializations`, `SourceScanPasses`, `SourceFilesScanned`, `RenderedSinkCount`, and `OutputSinkCount` SHALL be populated identically regardless of whether a `ValidationTiming` instance backed the run. `Counters.ContractFamilyCounts` is sourced from that `ValidationTiming` instance's own per-family counts and SHALL be empty when none was supplied — the CLI's `--profile` option and the Testing API's `WithProfile()` always supply one internally when profiling is requested (independent of whether `--timings`' human report was also requested), so this is only reachable through a direct `AnalysisProfileBuilder.Build` call with `timing: null`. `Counters.RenderedSinkCount` SHALL count only distinct normal-report formats whose rendering completed, never merely configured sinks; cancellation before a completed render therefore reports `0`. `Measurements` and each phase's elapsed/processor-time fields SHALL be `null` when no `ValidationTiming` instance backed the run, and populated when one did. Normal report routing SHALL expose `render_human`, `render_json`, `render_sarif`, `output_staging`, `output_stream_write`, and `output_commit` phases whenever the corresponding work occurs.

#### Scenario: Counters are identical whether or not timing is enabled
- **WHEN** the same validation request is profiled once with timing collection enabled and once without it
- **THEN** `Counters.PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, `ModesEvaluated`, `RenderedSinkCount`, and `OutputSinkCount` are identical between the two profiles, while `Counters.ContractFamilyCounts`, `Phases`, and `Measurements` are empty/`null` only in the profile built without timing

#### Scenario: Timing/resource fields never affect validation outcome
- **WHEN** a validation request is run once with `--profile` enabled and once without it
- **THEN** the produced findings, finding identity, ordering, and exit status are identical between the two runs

### Requirement: Counters prove the one-snapshot and sink-count-only invariants
The system SHALL populate `AnalysisProfile.Counters` from the same `ArchitectureAnalysisSnapshotCounters` instance the snapshot itself exposes (not a re-derived count), and SHALL record both the requested report sinks and their actual publication result. `FactIndexMaterializations` SHALL count lazy fact-index data builds for the retained snapshot and therefore never exceed one; `SourceScanPasses` and `SourceFilesScanned` SHALL describe files that passed generated-file exclusion and were successfully read for parsing. Contract-family result counts SHALL be recorded as each contract completes, so a snapshot observed to be cancelled during contract execution retains result counts from the completed contracts. Requesting additional output sinks for the same analysis SHALL change only the render/output counters, not `PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, snapshot/fact-index/source counters, or contract-family execution/result counts.

#### Scenario: An additional sink changes only render/output counters
- **WHEN** the same validation request is profiled once with one output sink and once with three output sinks
- **THEN** `Counters.RenderedSinkCount` and `Counters.OutputSinkCount` differ accordingly, while `Counters.PolicyCompositions`, `Counters.ProjectGraphEvaluations`, `Counters.AssemblyLoads`, and every contract-family count are identical between the two profiles

### Requirement: Cache and concurrency fields are populated when their capability is active
The system SHALL include `Counters.Cache` and `Counters.Concurrency` sub-records in `AnalysisProfile.Counters`. `Counters.Cache` SHALL report real `Lookups`, `Hits`, `Misses`, `Rejects`, `Writes`, `BytesRead`, `BytesWritten`, `IneligibleUnitCount`, `CorruptionEvents`, `CancelledBeforePublish`, `Mode` (`"disabled"`/`"auto"`/`"path"`, never a resolved absolute path), and `RejectReasonCounts` whenever the `analysis-cache` capability's `--cache`/`WithCache()` option is used with anything other than disabled, with `Status` set to `Active`; when the cache is disabled (the default), `Status` SHALL remain `NotApplicable` and every numeric field SHALL be `0`. `Counters.Concurrency` SHALL report the effective resolved max parallelism, scheduled and completed work-item counts, the observed maximum number of concurrently executing partition workers, and a deterministic-merge count, with `Status` set to `Active` whenever at least one scanning phase in the run actually took the bounded-parallel code path (partition count at or above the parallel-eligibility threshold and effective max parallelism greater than `1`); when every scanning phase in the run took the sequential path (a small target/source set, or `--max-parallelism 1`/`WithMaxParallelism(1)`), `Status` SHALL remain `NotApplicable` and every numeric field SHALL be `0`. Both sections SHALL use names and shapes stable enough for their owning capability to extend without renaming or restructuring them.

#### Scenario: Reserved fields report not-applicable today
- **WHEN** an `AnalysisProfile` is built for a run that did not enable the cache and whose scanning phases all took the sequential path (e.g. `--max-parallelism 1`)
- **THEN** `Counters.Cache.Status` and `Counters.Concurrency.Status` both equal `NotApplicable`, and their numeric fields equal `0`

#### Scenario: Cache-enabled run reports active status and real counters
- **WHEN** an `AnalysisProfile` is built for a run that enabled the cache via `--cache`/`WithCache()`
- **THEN** `Counters.Cache.Status` equals `Active`, `Counters.Cache.Mode` reflects the configured mode category, and `Writes`/`Rejects`/`RejectReasonCounts` reflect the real population attempt made for that run

#### Scenario: A parallel-eligible run reports active concurrency status and real counters
- **WHEN** an `AnalysisProfile` is built for a run whose target-assembly or source-root set is large enough to take the bounded-parallel scanning path at an effective max parallelism greater than `1`
- **THEN** `Counters.Concurrency.Status` equals `Active`, and its scheduled/completed work-item, observed-maximum-concurrency, and merge counters reflect the real scanning activity for that run

#### Scenario: A sequential-mode run reports not-applicable concurrency status
- **WHEN** an `AnalysisProfile` is built for a run with `--max-parallelism 1`/`WithMaxParallelism(1)`
- **THEN** `Counters.Concurrency.Status` equals `NotApplicable` regardless of how many target assemblies or source roots were scanned

### Requirement: The profile records a typed completion status including cancellation
The system SHALL include a `CompletionStatus` field on `AnalysisProfile` with exactly the values `Success`, `ValidationFailure`, `PreparationFailure`, and `Cancelled`, reflecting the actual outcome of the profiled run. `CompletionStatus.Cancelled` SHALL be used when cooperative cancellation was observed during the run, distinct from a generic failure. A profile marked `Cancelled` records that cancellation was observed; it makes no atomic-publication guarantee about the profile artifact file itself.

#### Scenario: A successful validation profile reports Success
- **WHEN** a validation run completes with no violations and no cancellation
- **THEN** the resulting `AnalysisProfile.CompletionStatus` equals `Success`

#### Scenario: A cancelled run reports Cancelled, not a generic failure
- **WHEN** cooperative cancellation is observed during a profiled validation run
- **THEN** the resulting `AnalysisProfile.CompletionStatus` equals `Cancelled`, and this is distinguishable in the serialized JSON from `ValidationFailure`/`PreparationFailure`

#### Scenario: Report publication failure preserves the completed analysis status
- **WHEN** validation completes successfully but a requested report sink fails to publish
- **THEN** `CompletionStatus` remains `Success`, `Output.OutputFailed` is `true`, and the CLI reports its runtime output error without classifying the run as `PreparationFailure`

### Requirement: CLI exposes the profile via a dedicated opt-in option
The system SHALL provide a `--profile <stdout|stderr|file-path>` option on the CLI `validate` command, independent of `--timings` and `--report`, that writes the `AnalysisProfile` as deterministic JSON to the requested destination. Omitting `--profile` SHALL leave command behavior, output, and exit code completely unchanged from before this capability existed.

#### Scenario: Profile output does not appear unless requested
- **WHEN** the CLI `validate` command runs without `--profile`
- **THEN** no profile document is written, and command output/exit code are unchanged from before this capability existed

#### Scenario: Profile can be written alongside any --format/--report combination
- **WHEN** the CLI `validate` command runs with `--profile stdout` together with `--format json` or one or more `--report` sinks
- **THEN** the profile JSON is written to stdout in addition to the requested format/report output, and the existing `--format`/`--report` output is unchanged

### Requirement: Testing API exposes the same profile semantics as the CLI
The system SHALL let `ArchLinterNet.Testing` consumers opt into profile collection via `ArchitectureValidationBuilder.WithProfile()` (and the shared-snapshot `ArchitectureValidationSnapshotSession`) and read the resulting `AnalysisProfile` from `ArchitectureValidationResult.Profile`, built through the same `AnalysisProfileBuilder` and fed by the same `ArchitectureAnalysisSnapshotCounters` type the CLI's `--profile` option uses — one shared implementation in Core, not two independently maintained ones. `RenderedSinkCount`/`OutputSinkCount` are host-specific: the Testing API has no CLI-style output sinks, so it always reports `0` for both, distinct from the CLI's minimum of `1`.

#### Scenario: Testing API reports real snapshot-derived counters
- **WHEN** a policy is validated via `ArchitectureValidationBuilder.WithProfile()`
- **THEN** `Counters.PolicyCompositions` and `Counters.ProjectGraphEvaluations` reflect the actual snapshot built for that run (e.g. both equal `1` for ordinary single-mode preparation), and `Counters.ModesEvaluated` reflects every mode evaluated against a shared `CreateSnapshot()` session

#### Scenario: Profile is absent unless explicitly requested
- **WHEN** an `ArchitectureValidationBuilder` run does not call `WithProfile()`
- **THEN** `ArchitectureValidationResult.Profile` is `null`

### Requirement: A JSON Schema validates real generated profile output without registry publication
The system SHALL ship a JSON Schema document describing the `analysis-profile/v1` shape, validate a real profile generated by an actual validation run against that schema as part of the test suite, and publish that exact schema in the 0.5.1 packaged schema registry after its writer and generated-output validation exist. The registry entry SHALL report `supportsWrite: true` and `supportsRead: false` until a public profile document reader is implemented; schema validation of generated output SHALL NOT be represented as profile read support.

#### Scenario: A real generated profile validates against the schema
- **WHEN** a validation run produces an `AnalysisProfile` and it is serialized to JSON
- **THEN** the resulting document validates successfully against the exact packaged `analysis-profile/v1` JSON Schema

#### Scenario: The schema is registered as write-only
- **WHEN** `PackagedSchemaRegistry.List()` is called
- **THEN** it includes an `analysis-profile` entry with write support and without read support

### Requirement: A documented phase/counter dictionary defines the stability contract
The system SHALL provide a documented dictionary of every phase name and counter field in `AnalysisProfile`, describing its semantics, so later work can add counters (#365, #408) or compare pre/post-optimization evidence (#409) without redefining existing phase names or envelope ownership.

#### Scenario: Every phase name and counter in the model is documented
- **WHEN** the phase/counter dictionary is compared against the `AnalysisProfile` model's actual fields and the phase names `ValidationTiming` produces
- **THEN** every field and phase name has a corresponding documented entry

### Requirement: A repeatable benchmark harness produces checked-in pre-optimization evidence
The system SHALL provide a repeatable benchmark harness, excluded from the default correctness test run, that exercises the declared pre-optimization scenario matrix against a synthetic large multi-host fixture at least ten times per scenario. Before computing statistics, it SHALL verify every priming and measured sample's expected `CompletionStatus`, CLI exit category, and `Output.OutputFailed` state. It SHALL report consistently bounded analysis-only time (excluding build/preflight and rendering/publication phases), output time (the documented rendering/staging/stream/commit phase set), and command-total time, with median and p95 for each per scenario. The resulting checked-in evidence SHALL retain each sample's complete raw profile (including processor time, measurements, output evidence, and deterministic counters) in `docs/internal/analysis-profile-pre-optimization-baseline-results.json`, alongside observed environment metadata, and is explicitly not presented as a universal or hardware-independent performance guarantee.

#### Scenario: Benchmark harness is excluded from the correctness gate
- **WHEN** `make test` or `make acceptance` runs
- **THEN** the benchmark harness does not execute as part of that run

#### Scenario: Checked-in evidence discloses its environment and non-universality
- **WHEN** the pre-optimization evidence document is read
- **THEN** it states the observed reference environment and explicitly disclaims that the recorded medians/p95 are a universal speed contract

### Requirement: Reproducible final post-optimization release evidence is published
The system SHALL provide a repeatable, explicitly-invoked post-optimization benchmark harness that reuses the `analysis-profile/v1` envelope, phase boundaries, synthetic large multi-host fixture, and scenario semantics of the checked-in pre-optimization evidence. It SHALL publish separate checked-in machine-readable post-optimization evidence and a human-readable comparison report after `analysis-cache/v1` and bounded parallel scanning are available. The evidence SHALL state the reference hardware, operating system, runtime, configuration, exact source commit, executed CLI binary file version and SHA-256, and the CLI package ID, semantic version, and SHA-256 digest of the one packed `.nupkg` selected by the harness. It SHALL describe median and p95 figures only as evidence for that declared environment, never as a hardware-independent performance contract.

#### Scenario: Post-optimization evidence remains comparable to the baseline
- **WHEN** the checked-in pre- and post-optimization evidence are compared
- **THEN** each matching scenario uses the same phase boundaries, records at least ten valid samples, separates preparation from analysis and output time, and retains raw or deterministic summarized profile evidence

#### Scenario: Release documentation can consume the evidence without benchmarking
- **WHEN** release documentation reads the final evidence report
- **THEN** it can identify the declared environment, source identity, executed-binary identity, packed-package identity, matrix, median/p95 results, correctness evidence, and non-universality statement without running the hardware-sensitive harness

### Requirement: Post-optimization evidence proves cache and parallel correctness
The post-optimization harness SHALL measure cache-disabled, first-population,
and verified warm-hit executions; sequential execution and documented bounded
parallel execution; separate and combined strict/audit execution; and one- and
three-sink output execution. Before accepting any successful timing sample, it
SHALL verify the expected completion status, CLI exit category, and output
publication state. It SHALL prove that cached and uncached canonical findings
and ordering are equivalent; sequential and parallel canonical findings and
ordering are equivalent; combined execution performs one policy composition and
one analysis project evaluation (with a separately recorded post-build reload
when `--ensure-built` is requested), and no redundant target-assembly scan;
cache profiles
identify avoided work and deterministic hit/miss/reject reasons; and parallel
profiles expose bounded observed concurrency and resource measurements where
the platform supports them.

#### Scenario: A verified warm cache hit is measured only after population
- **WHEN** the post-optimization matrix measures cache behavior
- **THEN** it records disabled and first-population runs separately from warm
  hits, and accepts a warm-hit sample only when its profile reports the exact
  avoided work and a verified cache-hit outcome

#### Scenario: Parallel execution preserves canonical results
- **WHEN** sequential and bounded-parallel runs use the same immutable inputs
- **THEN** their canonical findings and ordering are identical, observed
  concurrency does not exceed the resolved bound, and their profiles retain
  explicit resource-metric availability information

#### Scenario: Unsuccessful runs do not become timing samples
- **WHEN** a cancellation, failure, partial publication, or incorrect exit
  category occurs during the post-optimization matrix
- **THEN** the run is excluded from successful timing statistics while its
  completion, cleanup, cache, and concurrency evidence remains recorded

### Requirement: Final release evidence is an attributable pre/post comparison
The final post-optimization evidence SHALL retain both strict and audit profiles for paired runs, raw wall-clock/allocation/resource samples, median and p95 summaries, exact source commit, executed CLI binary identity, CLI package ID/version/SHA-256 identity, explicit build configuration, and a #374 baseline-to-post-to-delta table. The harness SHALL fail rather than select zero or multiple matching CLI packages. It SHALL compare every cached sample with its uncached canonical baseline and every parallel sample with its sequential counterpart, including finding order, completion, exit/publication state, and deterministic counters.

#### Scenario: Release documentation consumes the comparison
- **WHEN** release documentation reads the checked-in final report
- **THEN** it can inspect raw profiles, distributions, source/binary/package identities, configuration, and baseline/post/delta without recalculating the dataset

