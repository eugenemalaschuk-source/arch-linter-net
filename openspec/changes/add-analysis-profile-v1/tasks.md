## 1. Core model

- [ ] 1.1 Add `src/ArchLinterNet.Core/Profiling/AnalysisProfileId.cs` with `AnalysisProfileId.V1 = "analysis-profile/v1"`.
- [ ] 1.2 Add `AnalysisProfileCompletionStatus` enum (`Success`, `ValidationFailure`, `PreparationFailure`, `Cancelled`).
- [ ] 1.3 Add `AnalysisProfileReservedFieldStatus` enum (`NotApplicable`) and `AnalysisProfileCacheCounters`/`AnalysisProfileConcurrencyCounters` reserved sub-records.
- [ ] 1.4 Add `AnalysisProfilePhaseMeasurement` record (`Name`, `Indent`, `Ordinal`, `Count`, nullable `ElapsedMs`).
- [ ] 1.5 Add `AnalysisProfileMeasurements` record (nullable `PeakWorkingSetBytes`, `AllocatedBytesTotal`).
- [ ] 1.6 Add `AnalysisProfileCounters` record extending `ArchitectureAnalysisSnapshotCounters` fields plus contract-family counts, `RenderedSinkCount`, `OutputSinkCount`, fact-index materializations, `Cache`, `Concurrency`.
- [ ] 1.7 Add `AnalysisProfile` top-level record (`SchemaId`, `CompletionStatus`, `CancellationObserved`, `Counters`, `Phases`, `Measurements`).
- [ ] 1.8 Add `AnalysisProfileBuilder` that assembles an `AnalysisProfile` from `ValidationTiming?`, `ArchitectureAnalysisSnapshotCounters`, sink counts, and completion status.
- [ ] 1.9 Add deterministic JSON serialization for `AnalysisProfile` (stable property order, camelCase or matching existing JSON conventions).

## 2. CLI integration

- [ ] 2.1 Add `--profile <stdout|stderr|file-path>` option to `ValidateCommandDefinition`/`ValidateCommandOptions`.
- [ ] 2.2 Wire profile building and writing into `ValidateCommandHandler` for single-mode and combined-mode paths, independent of `--timings`/`--report`.
- [ ] 2.3 Ensure `CompletionStatus.Cancelled` is set when cooperative cancellation is observed during the profiled run, reusing existing cancellation signals (no new atomic-publication machinery).
- [ ] 2.4 Verify omitting `--profile` leaves existing output/exit-code behavior unchanged.

## 3. Testing API mirror

- [ ] 3.1 Add `ArchitectureValidationBuilder.WithProfile()`.
- [ ] 3.2 Add `ArchitectureValidationResult.Profile` populated via the shared `AnalysisProfileBuilder`.

## 4. Schema

- [ ] 4.1 Author `schema/0.5.1/analysis-profile.schema.json` describing the `analysis-profile/v1` shape.
- [ ] 4.2 Add a test that generates a real profile from a real validation run and validates it against the schema file directly (not via `PackagedSchemaRegistry`).
- [ ] 4.3 Add a test asserting `PackagedSchemaRegistry.List()` does not yet include an `analysis-profile` entry.

## 5. Corpus extension

- [ ] 5.1 Add `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/large-multi-host/` (8 synthetic host projects + 2 shared library projects).
- [ ] 5.2 Register the new fixture in `CheckpointAScenarioManifest.json` per the corpus's documented extension rule.

## 6. Correctness tests

- [ ] 6.1 Test: counters identical whether timing is enabled or not (only `Measurements`/`ElapsedMs` differ).
- [ ] 6.2 Test: timing/resource fields never affect finding identity, ordering, or exit status.
- [ ] 6.3 Test: an additional output sink changes only render/output counters, not analysis counters.
- [ ] 6.4 Test: reserved cache/concurrency fields report `NotApplicable`/`0`.
- [ ] 6.5 Test: cancellation observed during a profiled run yields `CompletionStatus.Cancelled`.
- [ ] 6.6 Test: CLI profile and Testing API profile counters match for equivalent input.
- [ ] 6.7 Test: profile absent unless explicitly requested (CLI and Testing API).

## 7. Benchmark harness and evidence

- [ ] 7.1 Add `[Explicit]` NUnit harness under `tests/ArchLinterNet.Core.Tests/AnalysisProfile/` implementing the 7 declared scenarios against `large-multi-host`, 10 runs each, separating restore/build from analysis time, computing median/p95.
- [ ] 7.2 Confirm the harness is excluded from `rtk make test`/`rtk make acceptance`.
- [ ] 7.3 Run the harness for real on this development machine and record actual results.
- [ ] 7.4 Write `docs/internal/analysis-profile-pre-optimization-baseline.md` with the real results, environment metadata, and a non-universality disclaimer (mirroring `docs/internal/checkpoint-a-evidence.md`).

## 8. Documentation

- [ ] 8.1 Write `docs/internal/analysis-profile-dictionary.md` documenting every phase name and counter field.

## 9. Validation and spec sync

- [ ] 9.1 Run `rtk make fmt`.
- [ ] 9.2 Run `rtk make acceptance` and fix any issue-related failures.
- [ ] 9.3 Compare implementation against `specs/analysis-profile/spec.md`; adjust wording/scenarios to match actual behavior.
- [ ] 9.4 Run `openspec validate --all`.
- [ ] 9.5 Run `openspec archive add-analysis-profile-v1`.
- [ ] 9.6 Open the pull request closing #374.
