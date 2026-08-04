## MODIFIED Requirements

### Requirement: Cache and concurrency fields are explicitly reserved
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
