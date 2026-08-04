## ADDED Requirements

### Requirement: A versioned analysis-cache/v1 envelope is available
The system SHALL provide an `AnalysisCacheEntryV1` model identified by the constant schema id `analysis-cache/v1` (`AnalysisCacheEnvelope.SchemaId`), composed only of concrete, non-polymorphic record types (`AnalysisCacheProjectManifest`, `AnalysisCacheFactsV1`, `AnalysisCacheEntryCompletionStatus`), and a canonical `ContentDigest` computed by explicit ordinal field concatenation rather than reliance on JSON property ordering.

#### Scenario: Entry deserialization never executes arbitrary types
- **WHEN** a cache entry file is deserialized
- **THEN** only the closed set of concrete `AnalysisCacheEntryV1` record types can be constructed, with no polymorphic or `$type`-discriminated conversion

#### Scenario: Content digest is stable across canonicalization
- **WHEN** the same logical entry fields are provided in different construction order
- **THEN** the computed `ContentDigest` is identical

### Requirement: Cache location defaults are opt-in and never authored by content
The system SHALL default the persistent cache to disabled when no `--cache`/`WithCache()` option is supplied. `--cache auto` SHALL resolve to the platform user-cache namespace `ArchLinterNet/0.5.1/analysis-cache/v1` (`%LOCALAPPDATA%` on Windows; `$XDG_CACHE_HOME` or `~/.cache` elsewhere). `--cache <path>` SHALL use a caller-selected path after canonical containment/safety validation. Policy, fragment, baseline, snapshot, receipt, or cache content SHALL NOT select the cache location.

#### Scenario: Cache disabled by default
- **WHEN** `validate` runs without `--cache`
- **THEN** no cache lookup or population occurs and command output/exit code are unchanged from before this capability existed

#### Scenario: Auto resolves under the product/schema-version namespace
- **WHEN** `--cache auto` is used
- **THEN** the resolved root path ends with `ArchLinterNet/0.5.1/analysis-cache/v1` under the platform's user-cache directory

### Requirement: Cache location resolution rejects unsafe paths
The system SHALL reject an explicit cache path that is empty, resolves to a filesystem root, is an existing file, or is a symlink/reparse-point directory, raising `AnalysisCacheLocationRejectedException` before any cache I/O occurs.

#### Scenario: Filesystem root rejected
- **WHEN** `--cache /` (or a drive root on Windows) is used
- **THEN** the CLI reports a runtime error before analysis begins and no cache directory is created

#### Scenario: Symlinked directory rejected
- **WHEN** an explicit cache path is a symbolic link or reparse point
- **THEN** location resolution throws `AnalysisCacheLocationRejectedException` and no entry is read or written

### Requirement: Reuse authorization requires more than a fingerprint match
The system SHALL require every cache hit to prove, in addition to a matching `AnalysisCacheKey` digest: identical `AnalysisCacheEnvelope.ToolVersion`, matching `FormatVersion`/`SchemaId`, a verified `ContentDigest`, an original `AnalysisCacheEntryCompletionStatus.Success` completion, and — for every affected project — an identical, still-`VerifiedCacheEligible` `EvaluatedBuildInputManifestV1` digest (per #406). A cache entry SHALL be treated as untrusted optimization data until all checks pass.

#### Scenario: Matching key but ineligible project is rejected
- **WHEN** a stored entry's key digest matches but any of its project manifests is not `VerifiedCacheEligible`
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(IneligibleBuildInput)`, never a `Hit`

#### Scenario: Project manifest digest changed
- **WHEN** a stored entry's project manifest digest does not match the freshly recomputed manifest digest for the same project/context
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(ProjectSetMismatch)`

### Requirement: Miss and reject outcomes are typed and fail safe
The system SHALL classify every non-hit outcome as `Missing`, `Corrupt`, `Truncated`, `ForeignSchema`, `IncompatibleFormatVersion`, `IncompatibleToolVersion`, `KeyMismatch`, `IntegrityMismatch`, `ProjectSetMismatch`, `IneligibleBuildInput`, `IncompleteOriginalRun`, `Cancelled`, `PathUnsafe`, `SizeExceeded`, or `Disabled`. A miss or reject SHALL fall back to normal verified computation and SHALL NOT itself be reported as an unexplained execution failure.

#### Scenario: Corrupt entry falls back safely
- **WHEN** a stored entry file contains invalid JSON
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(Corrupt)` and the caller proceeds with ordinary verified computation

#### Scenario: An I/O failure while deriving the cache key never becomes an execution error
- **WHEN** the CLI or Testing API cannot read a policy/project file while deriving a cache key during population
- **THEN** the reject is recorded as `IneligibleBuildInput` and the already-completed validation result/exit code is unaffected

### Requirement: Persistence is atomic and cancellation-safe
The system SHALL stage each `Put` as a uniquely-named temporary file in the same directory as its target entry, verify `cancellationToken.IsCancellationRequested` is `false` immediately before publication, and publish via an atomic rename. A cancelled populate attempt SHALL delete its staged file and SHALL NOT publish a reusable entry.

#### Scenario: Cancellation before publication leaves no entry
- **WHEN** `AnalysisCacheStore.Put` is called with an already-cancelled `CancellationToken`
- **THEN** it returns `Cancelled`, no entry file is published, and a subsequent `TryGet` for the same key reports `Missing`

### Requirement: Inspect and clear operations are safe and deterministic
The system SHALL provide `AnalysisCacheStore.Inspect`/`Clear` operations that enumerate entries in deterministic ordinal order, never expose absolute cache-root paths in their output, never execute or deserialize entry content into unbounded live objects beyond the closed entry DTO, and refuse to operate when the resolved cache root is a filesystem root.

#### Scenario: Inspect output excludes the cache root path
- **WHEN** `cache inspect --cache <path>` runs
- **THEN** the JSON output contains entry-relative file names and stable facts, never the absolute cache root

#### Scenario: Clear refuses a filesystem root
- **WHEN** `AnalysisCacheStore.Clear` is called with a location whose root is a filesystem root
- **THEN** it throws `AnalysisCacheLocationRejectedException` and deletes nothing

### Requirement: CLI and Testing API share one cache implementation
The system SHALL expose equivalent disabled/auto/explicit-path cache semantics through the CLI's `--cache` option and `ArchLinterNet.Testing`'s `ArchitectureValidationBuilder.WithCache()`, both backed by the same `ArchLinterNet.Core.Caching.AnalysisCacheStore`/`AnalysisCachePopulation` implementation.

#### Scenario: CLI and Testing populate identically
- **WHEN** the CLI and the Testing API validate the same policy with the same `--cache`/`WithCache()` location and configuration
- **THEN** they derive the same `AnalysisCacheKey` digest and observe the same authorization outcome

### Requirement: Cache population is gated on completed, non-cancelled, eligible runs
The system SHALL populate a cache entry only after a completed, non-cancelled run whose every discovered project's #406 evaluated-build-input manifest reports `VerifiedCacheEligible`. Given the current `EvaluatedBuildInputManifestCollector`'s intentional fail-closed behavior (always `CacheIneligible` for real MSBuild evidence), population against real discovered projects SHALL always report `IneligibleBuildInput` today; this is expected and SHALL NOT be worked around by relaxing the eligibility gate.

#### Scenario: A real discovered project is never populated today
- **WHEN** `AnalysisCachePopulation.TryPopulate` is called with a real project discovered from an actual `.csproj`
- **THEN** it returns `IneligibleBuildInput` and no entry file is written

### Requirement: analysis-profile/v1 instrumentation reflects real cache activity
The system SHALL populate `AnalysisProfile.Counters.Cache` with real `Lookups`/`Hits`/`Misses`/`Rejects`/`Writes`/`BytesRead`/`BytesWritten`/`IneligibleUnitCount`/`CorruptionEvents`/`CancelledBeforePublish`/`Mode`/`RejectReasonCounts` whenever `--cache`/`WithCache()` is used with anything other than disabled, setting `Status` to `Active`; `Status` SHALL remain `NotApplicable` (all fields at their zero/default value) when the cache is disabled.

#### Scenario: Cache-enabled run reports Active status
- **WHEN** a `validate --cache auto --profile stdout` run completes
- **THEN** the profile's `Counters.Cache.Status` equals `Active` and `Counters.Cache.Mode` equals `"auto"`

#### Scenario: Cache-disabled run reports NotApplicable
- **WHEN** a `validate --profile stdout` run completes without `--cache`
- **THEN** the profile's `Counters.Cache.Status` equals `NotApplicable`
