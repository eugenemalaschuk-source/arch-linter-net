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

### Requirement: Cache and concurrency fields are explicitly reserved
The system SHALL include `Counters.Cache` and `Counters.Concurrency` sub-records in `AnalysisProfile.Counters` with all numeric fields set to `0` and an explicit `NotApplicable` status, since no persistent cache (#365) or parallel scanning (#408) exists yet. These fields SHALL use names and shapes stable enough for #365/#408 to populate with real values later without renaming or restructuring them.

#### Scenario: Reserved fields report not-applicable today
- **WHEN** any `AnalysisProfile` is built by the current implementation
- **THEN** `Counters.Cache.Status` and `Counters.Concurrency.Status` both equal `NotApplicable`, and their numeric fields equal `0`

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
The system SHALL ship a JSON Schema document describing the `analysis-profile/v1` shape, and SHALL validate a real profile generated by an actual validation run against that schema as part of the test suite. This schema SHALL NOT be registered in the packaged schema registry's compatibility manifest by this capability; registry publication is owned by a later change.

#### Scenario: A real generated profile validates against the schema
- **WHEN** a validation run produces an `AnalysisProfile` and it is serialized to JSON
- **THEN** the resulting document validates successfully against the shipped `analysis-profile/v1` JSON Schema

#### Scenario: The schema is not yet registered in the packaged registry
- **WHEN** `PackagedSchemaRegistry.List()` is called
- **THEN** it does not include an `analysis-profile` entry

### Requirement: A documented phase/counter dictionary defines the stability contract
The system SHALL provide a documented dictionary of every phase name and counter field in `AnalysisProfile`, describing its semantics, so later work can add counters (#365, #408) or compare pre/post-optimization evidence (#409) without redefining existing phase names or envelope ownership.

#### Scenario: Every phase name and counter in the model is documented
- **WHEN** the phase/counter dictionary is compared against the `AnalysisProfile` model's actual fields and the phase names `ValidationTiming` produces
- **THEN** every field and phase name has a corresponding documented entry

### Requirement: A repeatable benchmark harness produces checked-in pre-optimization evidence
The system SHALL provide a repeatable benchmark harness, excluded from the default correctness test run, that exercises the declared pre-optimization scenario matrix against a synthetic large multi-host fixture at least ten times per scenario, verifies the expected status of every priming and measured sample before computing statistics, separates restore/build time from analysis time, and computes median and p95 elapsed time per scenario. The resulting checked-in evidence SHALL retain each sample's complete raw profile (including processor time, measurements, output evidence, and deterministic counters), together with the observed environment metadata, and is explicitly not presented as a universal or hardware-independent performance guarantee.

#### Scenario: Benchmark harness is excluded from the correctness gate
- **WHEN** `rtk make test` or `rtk make acceptance` runs
- **THEN** the benchmark harness does not execute as part of that run

#### Scenario: Checked-in evidence discloses its environment and non-universality
- **WHEN** the pre-optimization evidence document is read
- **THEN** it states the observed reference environment and explicitly disclaims that the recorded medians/p95 are a universal speed contract
