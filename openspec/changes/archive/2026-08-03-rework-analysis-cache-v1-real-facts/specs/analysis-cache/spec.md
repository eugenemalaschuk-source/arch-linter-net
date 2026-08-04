## MODIFIED Requirements

### Requirement: A versioned analysis-cache/v1 envelope is available
The system SHALL provide an `AnalysisCacheEntryV1` model identified by the constant schema id `analysis-cache/v1` (`AnalysisCacheEnvelope.SchemaId`), composed only of concrete, non-polymorphic record types (`AnalysisCacheProjectManifest`, `AnalysisCacheOutcomeV1`, `AnalysisCacheEntryCompletionStatus`) plus one explicit closed-set converter (`AnalysisCacheDiagnosticPayloadConverter`) for `IArchitectureDiagnosticPayload`'s 18 concrete record types, and a canonical `ContentDigest` computed by explicit ordinal field concatenation for scalar/manifest fields and a deterministic canonical-JSON hash for the nested `Outcome`, rather than reliance on JSON property ordering. Each entry is scoped to exactly one requested mode (`AnalysisCacheEntryV1.Mode`), never a joined mode set.

#### Scenario: Entry deserialization never executes arbitrary types
- **WHEN** a cache entry file is deserialized
- **THEN** only the closed set of concrete `AnalysisCacheEntryV1` record types — including the 18 known `IArchitectureDiagnosticPayload` types discriminated by an explicit `$kind` switch — can be constructed, with no polymorphic or `$type`-discriminated conversion, and an unrecognized `$kind` value raises `JsonException` rather than constructing anything

#### Scenario: Content digest is stable across canonicalization
- **WHEN** the same logical entry fields are provided in different construction order
- **THEN** the computed `ContentDigest` is identical

#### Scenario: A hit reconstructs byte-identical findings, ordering, and exit category
- **WHEN** `AnalysisCacheStore.TryGet` returns `Hit` for a key whose `Outcome` was populated from a prior completed run's `ValidationOutcome`
- **THEN** `AnalysisCacheOutcomeMapper.FromCacheOutcome` reconstructs a `ValidationOutcome` whose `Violations` (including `Payload`/`Identity`), `Cycles`, `UnmatchedIgnoredViolations`, `PolicyConsistencyFindings`, `ClassificationConflicts`/`ClassificationMetadataFailures`, and `Passed` are exactly what was cached, in the same order

### Requirement: Cache location resolution rejects unsafe paths
The system SHALL reject an explicit cache path that is empty, resolves to a filesystem root, is an existing file, or is a symlink/reparse-point directory, raising `AnalysisCacheLocationRejectedException` before any cache I/O occurs. In addition, every `AnalysisCacheStore` read/write/inspect/clear operation SHALL reject a resolved entry path whose containing shard directory (or any existing ancestor between it and the cache root) is itself a reparse point, checked immediately before touching the filesystem, and `Inspect`/`Clear` SHALL enumerate the cache root's contents without following a symlinked subdirectory.

#### Scenario: Filesystem root rejected
- **WHEN** `--cache /` (or a drive root on Windows) is used
- **THEN** the CLI reports a runtime error before analysis begins and no cache directory is created

#### Scenario: Symlinked directory rejected
- **WHEN** an explicit cache path is a symbolic link or reparse point
- **THEN** location resolution throws `AnalysisCacheLocationRejectedException` and no entry is read or written

#### Scenario: Symlinked shard directory does not escape containment
- **WHEN** a shard directory under an otherwise-valid cache root is pre-created as a symlink or junction pointing outside the root
- **THEN** `AnalysisCacheStore.Put`/`TryGet` reject the operation as `PathUnsafe` and no file is read from or written to the link's target

#### Scenario: Inspect and Clear do not follow symlinked subdirectories
- **WHEN** a subdirectory under the cache root is a symlink or junction
- **THEN** `AnalysisCacheStore.Inspect`/`Clear` do not enumerate, read, or delete anything through it

### Requirement: analysis-profile/v1 instrumentation reflects real cache activity
The system SHALL populate `AnalysisProfile.Counters.Cache` with real `Lookups`/`Hits`/`Misses`/`Rejects`/`Writes`/`BytesRead`/`BytesWritten`/`IneligibleUnitCount`/`CorruptionEvents`/`CancelledBeforePublish`/`Mode`/`RejectReasonCounts` whenever `--cache`/`WithCache()` is used with anything other than disabled, setting `Status` to `Active`; `Status` SHALL remain `NotApplicable` (all fields at their zero/default value) when the cache is disabled. `Lookups`/`Hits`/`Misses`/`BytesRead` SHALL be sourced from real `AnalysisCachePopulation.TryLookup` calls made by the cache-hit short-circuit, and `IneligibleUnitCount`/`BytesWritten`/`CorruptionEvents` SHALL be sourced from real `AnalysisCachePopulation`/`AnalysisCacheStore` outcomes, never left hardcoded at zero when the underlying activity was non-zero.

#### Scenario: Cache-enabled run reports Active status
- **WHEN** a `validate --cache auto --profile stdout` run completes
- **THEN** the profile's `Counters.Cache.Status` equals `Active` and `Counters.Cache.Mode` equals `"auto"`

#### Scenario: Cache-disabled run reports NotApplicable
- **WHEN** a `validate --profile stdout` run completes without `--cache`
- **THEN** the profile's `Counters.Cache.Status` equals `NotApplicable`

#### Scenario: Ineligible projects are counted, not just rejected
- **WHEN** a `--cache`-enabled run discovers at least one project whose #406 evaluated-build-input manifest is not `VerifiedCacheEligible`
- **THEN** `Counters.Cache.IneligibleUnitCount` is greater than zero

## ADDED Requirements

### Requirement: The cache-hit short-circuit skips mode-specific evaluation work
The system SHALL attempt a cache lookup for each requested mode before running that mode's contract execution, coverage, classification, and policy-consistency checks, whenever a cache location was configured for the request. A `Hit` SHALL reconstruct the mode's `ValidationOutcome` from the cached `AnalysisCacheOutcomeV1` instead of running that work; a `Miss`/`Reject` SHALL fall through to the unchanged full pipeline for that mode. Policy composition, project discovery, and assembly loading SHALL still run unconditionally (required to establish per-project #406 eligibility and shared by every requested mode), so this requirement bounds the mode-specific evaluation phase only, not snapshot construction.

#### Scenario: A hit skips contract execution for that mode
- **WHEN** `ArchitectureAnalysisSnapshot.Evaluate(mode)` is called with a configured cache location and the lookup for that mode reports `Hit`
- **THEN** the contract executor is never invoked for that mode and the returned `ValidationOutcome` comes from `AnalysisCacheOutcomeMapper.FromCacheOutcome`

#### Scenario: A miss or reject runs the full pipeline unchanged
- **WHEN** the lookup for a mode reports `Miss` or `Reject`
- **THEN** `EvaluateCore` runs exactly as it did before the cache-hit short-circuit existed, and the outcome is unaffected by the cache having been consulted

### Requirement: Cache key identity is portable across checkouts and separates workspace binding
The system SHALL derive `AnalysisCacheKey`'s digest only from checkout-independent inputs (schema/format/tool identity, mode, condition set, contract-id set, configuration/TFM/platform/RID) and repository-relative content digests (`PolicyDigest`, hashed from paths relative to the repository root, never an absolute checkout path). A separate `WorkspaceDigest` (also repository-relative: a sorted digest of discovered project paths) SHALL provide workspace/trust-domain binding as an independent control, not folded into portability.

#### Scenario: Equivalent repository content produces the same key at different checkout roots
- **WHEN** equivalent policy content and discovered project paths are evaluated under two different absolute checkout roots
- **THEN** `AnalysisCacheKey.ComputePolicyDigest`/`ComputeWorkspaceDigest` produce identical digests for both

#### Scenario: Repository root is never hashed
- **WHEN** an `AnalysisCacheKey.Digest` is computed
- **THEN** no absolute filesystem path appears among its inputs

### Requirement: Cache entries are scoped one per requested mode
The system SHALL populate and look up exactly one `AnalysisCacheEntryV1` per requested mode; a combined-mode request (e.g. `strict,audit`) SHALL never collapse more than one mode's outcome under a single key, and one mode's stored `Passed`/findings SHALL never be attributed to another mode.

#### Scenario: Strict and audit populate independent entries
- **WHEN** a combined `strict,audit` validation run completes with strict passing and audit failing
- **THEN** the cache contains two entries, the strict entry's `Outcome.Passed` is `true`, and the audit entry's `Outcome.Passed` is `false`

### Requirement: The write side enforces the same size bound as the read side
The system SHALL check the serialized entry's byte length (and project-manifest count) against the same bound `TryGet` enforces before writing anything to disk, returning `PutResult.Rejected(SizeExceeded)` when exceeded instead of publishing an entry every subsequent read would reject.

#### Scenario: An oversized outcome is rejected before any file is written
- **WHEN** `AnalysisCacheStore.Put` is called with an `AnalysisCacheOutcomeV1` whose serialized form exceeds the entry size bound
- **THEN** it returns `PutResult.Rejected(SizeExceeded)` and no entry or temp file is left on disk
