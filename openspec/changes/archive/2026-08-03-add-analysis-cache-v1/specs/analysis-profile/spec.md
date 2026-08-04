## MODIFIED Requirements

### Requirement: Cache and concurrency fields are explicitly reserved
The system SHALL include `Counters.Cache` and `Counters.Concurrency` sub-records in `AnalysisProfile.Counters`. `Counters.Concurrency` SHALL keep all numeric fields at `0` and `Status` at `NotApplicable`, since parallel scanning (#408) does not exist yet. `Counters.Cache` SHALL report real `Lookups`, `Hits`, `Misses`, `Rejects`, `Writes`, `BytesRead`, `BytesWritten`, `IneligibleUnitCount`, `CorruptionEvents`, `CancelledBeforePublish`, `Mode` (`"disabled"`/`"auto"`/`"path"`, never a resolved absolute path), and `RejectReasonCounts` whenever the `analysis-cache` capability's `--cache`/`WithCache()` option is used with anything other than disabled, with `Status` set to `Active`; when the cache is disabled (the default), `Status` SHALL remain `NotApplicable` and every numeric field SHALL be `0`. Both sections SHALL use names and shapes stable enough for their owning capability to extend without renaming or restructuring them.

#### Scenario: Reserved fields report not-applicable today
- **WHEN** an `AnalysisProfile` is built for a run that did not enable the cache
- **THEN** `Counters.Cache.Status` and `Counters.Concurrency.Status` both equal `NotApplicable`, and their numeric fields equal `0`

#### Scenario: Cache-enabled run reports active status and real counters
- **WHEN** an `AnalysisProfile` is built for a run that enabled the cache via `--cache`/`WithCache()`
- **THEN** `Counters.Cache.Status` equals `Active`, `Counters.Cache.Mode` reflects the configured mode category, and `Writes`/`Rejects`/`RejectReasonCounts` reflect the real population attempt made for that run
