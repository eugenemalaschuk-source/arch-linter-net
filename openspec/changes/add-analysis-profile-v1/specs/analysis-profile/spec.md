## ADDED Requirements

### Requirement: A versioned, machine-readable analysis profile is available
The system SHALL provide an `AnalysisProfile` model identified by the constant schema id `analysis-profile/v1` (`AnalysisProfileId.V1`), buildable from an existing `ValidationTiming?`, `ArchitectureAnalysisSnapshotCounters`, sink render/output counts, and a completion status, without modifying `ValidationTiming`'s existing human-readable report shape or `ArchitectureAnalysisSnapshotCounters`'s existing fields.

#### Scenario: Building a profile does not change existing timing/counter behavior
- **WHEN** an `AnalysisProfile` is built from a `ValidationTiming` instance that also has `WriteReport` called on it
- **THEN** the human-readable timing report's phase names, order, and values are identical to what they would be without building a profile

### Requirement: Deterministic counters are separated from environment-dependent measurements
The system SHALL group `AnalysisProfile` fields into a deterministic `Counters` section (policy compositions, project-graph evaluations, assembly loads, modes evaluated, contract-family execution/result counts, render count, output-sink count, fact-index materializations) and a nullable `Measurements` section (peak working set bytes, allocated bytes) plus nullable per-phase `ElapsedMs`. Deterministic counters SHALL be populated identically regardless of whether timing collection is enabled; nullable measurement fields SHALL be `null` when the underlying `ValidationTiming` is not supplied, and non-null when it is.

#### Scenario: Counters are identical whether or not timing is enabled
- **WHEN** the same validation request is profiled once with timing collection enabled and once without it
- **THEN** every field under `Counters` is identical between the two profiles, and only `Measurements` and per-phase `ElapsedMs` differ (present vs. `null`)

#### Scenario: Timing/resource fields never affect validation outcome
- **WHEN** a validation request is run once with `--profile` enabled and once without it
- **THEN** the produced findings, finding identity, ordering, and exit status are identical between the two runs

### Requirement: Counters prove the one-snapshot and sink-count-only invariants
The system SHALL populate `AnalysisProfile.Counters` from the same `ArchitectureAnalysisSnapshotCounters` instance the snapshot itself exposes (not a re-derived count), and SHALL record the number of report sinks rendered and written for the run. Requesting additional output sinks for the same analysis SHALL change only the render/output counters, not `PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, or contract-family execution counts.

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

### Requirement: CLI exposes the profile via a dedicated opt-in option
The system SHALL provide a `--profile <stdout|stderr|file-path>` option on the CLI `validate` command, independent of `--timings` and `--report`, that writes the `AnalysisProfile` as deterministic JSON to the requested destination. Omitting `--profile` SHALL leave command behavior, output, and exit code completely unchanged from before this capability existed.

#### Scenario: Profile output does not appear unless requested
- **WHEN** the CLI `validate` command runs without `--profile`
- **THEN** no profile document is written, and command output/exit code are unchanged from before this capability existed

#### Scenario: Profile can be written alongside any --format/--report combination
- **WHEN** the CLI `validate` command runs with `--profile stdout` together with `--format json` or one or more `--report` sinks
- **THEN** the profile JSON is written to stdout in addition to the requested format/report output, and the existing `--format`/`--report` output is unchanged

### Requirement: Testing API exposes the same profile semantics as the CLI
The system SHALL let `ArchLinterNet.Testing` consumers opt into profile collection via `ArchitectureValidationBuilder.WithProfile()` and read the resulting `AnalysisProfile` from `ArchitectureValidationResult.Profile`, built through the same `AnalysisProfileBuilder` the CLI uses, so a profile obtained through the Testing API and one obtained through the CLI for equivalent inputs contain identical `Counters`.

#### Scenario: Testing API profile matches CLI profile counters for equivalent input
- **WHEN** the same policy and target assemblies are validated once via the CLI with `--profile` and once via `ArchitectureValidationBuilder.WithProfile()`
- **THEN** both profiles' `Counters` sections are identical

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
The system SHALL provide a repeatable benchmark harness, excluded from the default correctness test run, that exercises the declared pre-optimization scenario matrix against a synthetic large multi-host fixture at least ten times per scenario, separates restore/build time from analysis time, and computes median and p95 elapsed time per scenario. The resulting evidence, together with the observed environment metadata, SHALL be checked into the repository as descriptive baseline evidence, explicitly not presented as a universal or hardware-independent performance guarantee.

#### Scenario: Benchmark harness is excluded from the correctness gate
- **WHEN** `rtk make test` or `rtk make acceptance` runs
- **THEN** the benchmark harness does not execute as part of that run

#### Scenario: Checked-in evidence discloses its environment and non-universality
- **WHEN** the pre-optimization evidence document is read
- **THEN** it states the observed reference environment and explicitly disclaims that the recorded medians/p95 are a universal speed contract
