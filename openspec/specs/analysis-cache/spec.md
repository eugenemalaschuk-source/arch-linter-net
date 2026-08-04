# analysis-cache Specification

## Purpose

Define the opt-in, disabled-by-default persistent `analysis-cache/v1` used by issue #365: a versioned, cancellation-safe, integrity-verified storage and reuse-authorization engine that consumes #406's evaluated-build-input manifest eligibility and #375's cancellation-safe publication semantics, and extends `analysis-profile/v1`'s already-reserved `Counters.Cache` section with real instrumentation. It caches deterministic project/output facts and eligibility evidence, never a bare `passed` result and never polymorphic finding detail, and it must never turn a miss, reject, corruption, or cancellation into a false success or an unexplained execution failure.
## Requirements
### Requirement: A versioned analysis-cache/v1 envelope is available
The system SHALL provide an `AnalysisCacheEntryV1` model identified by the constant schema id `analysis-cache/v1` (`AnalysisCacheEnvelope.SchemaId`), composed only of concrete, non-polymorphic record types (`AnalysisCacheProjectManifest`, `AnalysisCacheArtifactManifest`, `AnalysisCacheOutcomeV1`, `AnalysisCacheEntryCompletionStatus`) plus one explicit closed-set converter (`AnalysisCacheDiagnosticPayloadConverter`) for `IArchitectureDiagnosticPayload`'s 18 concrete record types and one explicit closed-set converter (`AnalysisCacheClassificationMetadataValueConverter`) for `ArchitectureClassificationRoleFact.Metadata`'s string/bool/decimal `object` values. Format version 2 SHALL include ordered artifact byte manifests for every selected PE, its PDB, and its build receipt, plus every PE materialized from an isolated post-build load scope's exact/probing-path reference closure and each such artifact's PDB/receipt. For stream-loaded assemblies, the PE/PDB bytes SHALL be captured once into a bounded immutable buffer; the digest and `AssemblyLoadContext.LoadFromStream` SHALL both consume that same buffer, never separate reads of a mutable path. The cache SHALL receive these authoritative physical paths from resolution/load-scope inventories, never infer stream-loaded artifact identity from `Assembly.Location`. A non-isolated/default-context resolution without an equivalent exact-byte root-and-reference inventory SHALL be cache-ineligible. Public compatibility helpers that cannot accept authoritative artifact evidence SHALL reject rather than read or write an artifact-less entry. The canonical `ContentDigest` SHALL use explicit ordinal field concatenation for scalar/project/artifact-manifest fields and a deterministic canonical-JSON hash for the nested `Outcome`, rather than reliance on JSON property ordering. Each entry is scoped to exactly one requested mode (`AnalysisCacheEntryV1.Mode`), never a joined mode set. `AnalysisCacheOutcomeV1` SHALL carry every result-bearing field a cache hit must reconstruct: `Passed`, `Violations`, `Cycles`, `CoverageFindings`/`CoverageConfig`, `UnmatchedIgnoredViolations`/`UnmatchedIgnoredViolationsConfig`, `PolicyConsistencyFindings`/`PolicyConsistencyConfig`, `ClassificationConflicts`, `ClassificationMetadataFailures`, `ClassificationRoles`, `ClassificationPathDeferred`, `CycleFindings`, `CoverageSummaries`, and `SubtractiveMatcherParticipation`.

#### Scenario: Entry deserialization never executes arbitrary types
- **WHEN** a cache entry file is deserialized
- **THEN** only the closed set of concrete `AnalysisCacheEntryV1` record types — including the 18 known `IArchitectureDiagnosticPayload` types discriminated by an explicit `$kind` switch, and classification metadata values restricted to string/bool/decimal — can be constructed, with no polymorphic or `$type`-discriminated conversion, and an unrecognized `$kind` value or metadata value shape raises `JsonException` rather than constructing anything

#### Scenario: Content digest is stable across canonicalization
- **WHEN** the same logical entry fields are provided in different construction order
- **THEN** the computed `ContentDigest` is identical

#### Scenario: A hit reconstructs byte-identical findings, ordering, and exit category
- **WHEN** `AnalysisCacheStore.TryGet` returns `Hit` for a key whose `Outcome` was populated from a prior completed run's `ValidationOutcome`
- **THEN** `AnalysisCacheOutcomeMapper.FromCacheOutcome` reconstructs a `ValidationOutcome` whose `Violations` (including `Payload`/`Identity`), `Cycles`, `CycleFindings`, `UnmatchedIgnoredViolations`, `PolicyConsistencyFindings`, `ClassificationConflicts`/`ClassificationMetadataFailures`/`ClassificationRoles`/`ClassificationPathDeferred`, `CoverageSummaries`, `SubtractiveMatcherParticipation`, and `Passed` are exactly what was cached, in the same order

#### Scenario: A changed selected artifact invalidates reuse
- **WHEN** the bytes of a selected PE, matching PDB, or build receipt differ from the pre-run artifact manifest
- **THEN** the candidate cache entry is rejected as `ArtifactSetMismatch`, never a `Hit`

#### Scenario: A changed post-build reference artifact invalidates reuse
- **WHEN** an isolated post-build load scope materialized a reference assembly from its exact/probing paths and that reference PE, PDB, or receipt differs from the pre-run artifact manifest
- **THEN** the candidate cache entry is rejected as `ArtifactSetMismatch`, never a `Hit`

#### Scenario: Artifact-less compatibility helpers fail closed
- **WHEN** a caller invokes a public cache lookup or population helper that cannot provide authoritative selected and loaded artifact evidence
- **THEN** it returns `IneligibleBuildInput` and neither reads nor writes an artifact-less cache entry

### Requirement: Cache location defaults are opt-in and never authored by content
The system SHALL default the persistent cache to disabled when no `--cache`/`WithCache()` option is supplied. `--cache auto` SHALL resolve to the platform user-cache namespace `ArchLinterNet/0.5.1/analysis-cache/v1` (`%LOCALAPPDATA%` on Windows; `$XDG_CACHE_HOME` or `~/.cache` elsewhere). `--cache <path>` SHALL use a caller-selected path after canonical containment/safety validation. Policy, fragment, baseline, snapshot, receipt, or cache content SHALL NOT select the cache location.

#### Scenario: Cache disabled by default
- **WHEN** `validate` runs without `--cache`
- **THEN** no cache lookup or population occurs and command output/exit code are unchanged from before this capability existed

#### Scenario: Auto resolves under the product/schema-version namespace
- **WHEN** `--cache auto` is used
- **THEN** the resolved root path ends with `ArchLinterNet/0.5.1/analysis-cache/v1` under the platform's user-cache directory

### Requirement: Cache location resolution rejects unsafe paths
The system SHALL reject an explicit cache path that is empty, resolves to a filesystem root, is an existing file, or is a symlink/reparse-point directory, raising `AnalysisCacheLocationRejectedException` before any cache I/O occurs. In addition, every `AnalysisCacheStore` read/write/inspect/clear operation SHALL reject a resolved entry path whose containing shard directory (or any existing ancestor between it and the cache root) is itself a reparse point, checked immediately before touching the filesystem, and `Inspect`/`Clear` SHALL enumerate the cache root's contents without following a symlinked subdirectory. Every `AnalysisCacheStore` operation (`TryGet`, `Put`, `Inspect`, `Clear`) SHALL additionally reject the cache root itself being a symlink/reparse-point directory, checked before any `Directory.Exists`/enumeration/I/O, regardless of `AnalysisCacheMode` — including `Auto`, whose location resolution does not run `ExplicitPath`'s own root-symlink validation.

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

#### Scenario: A pre-created symlinked cache root is rejected in every mode
- **WHEN** a cache root path (resolved via any `AnalysisCacheMode`, including `Auto`) is itself a pre-created symlink or junction pointing outside the intended root
- **THEN** `AnalysisCacheStore.Inspect`/`Clear`/`TryGet`/`Put` reject or no-op without following it, and `Clear` deletes nothing under the link's target

### Requirement: Reuse authorization requires more than a fingerprint match
The system SHALL require every cache hit to prove, in addition to a matching `AnalysisCacheKey` digest: identical `AnalysisCacheEnvelope.ToolVersion`, matching `FormatVersion`/`SchemaId`, a verified keyed-HMAC `ContentDigest`, an original `AnalysisCacheEntryCompletionStatus.Success` completion, identical artifact-byte manifests, and — for every affected project — an identical, still-`VerifiedCacheEligible` `EvaluatedBuildInputManifestV1` digest (per #406), compared as genuine one-to-one ordered-set equality after rejecting any duplicate `ProjectPath` on either side (a forged stored list with a duplicate path SHALL NOT authorize against a distinct current project set merely by matching count). `AnalysisCacheKey` SHALL additionally fold in every remaining result-affecting request dimension: an order-independent `PreprocessorSymbolsDigest`, a content-based `BaselineDigest` (never the baseline's path), `IncludeAsmdefContracts`, and `EnforceUnmatchedIgnoredViolationsPolicy`. Policy imports and an optional baseline SHALL contribute identities captured from the exact decoded text that was parsed and merged into the snapshot; their mutable paths SHALL be revalidated before a lookup is accepted. The lookup path (`ArchitectureAnalysisSnapshot.TryEvaluateFromCache`) SHALL thread the session's `CancellationToken` through its policy-digest and manifest-lookup computation and SHALL NOT accept a `Hit` once cancellation has been observed. A cache entry SHALL be treated as untrusted optimization data until all checks pass.

#### Scenario: Matching key but ineligible project is rejected
- **WHEN** a stored entry's key digest matches but any of its project manifests is not `VerifiedCacheEligible`
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(IneligibleBuildInput)`, never a `Hit`

#### Scenario: Project manifest digest changed
- **WHEN** a stored entry's project manifest digest does not match the freshly recomputed manifest digest for the same project/context
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(ProjectSetMismatch)`

#### Scenario: A duplicated stored project path does not authorize against a distinct current set
- **WHEN** a stored manifest list contains a duplicate `ProjectPath` (e.g. `[A, A]`) while the current discovered set is genuinely different (e.g. `[A, B]`)
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(ProjectSetMismatch)`, never a `Hit`

#### Scenario: A request differing only in preprocessor symbols, baseline content, asmdef inclusion, or unmatched-ignore enforcement derives a different key
- **WHEN** two otherwise-identical requests differ only in `PreprocessorSymbols`, baseline file content, `IncludeAsmdefContracts`, or `EnforceUnmatchedIgnoredViolationsPolicy`
- **THEN** their `AnalysisCacheKey.Digest` values differ

#### Scenario: A cancelled lookup never returns a stale hit
- **WHEN** the session's `CancellationToken` is observed as cancelled during or immediately after `TryEvaluateFromCache`'s lookup
- **THEN** the lookup is not accepted as a `Hit` and evaluation falls back to (and observes cancellation via) the normal recomputation path

### Requirement: Miss and reject outcomes are typed and fail safe
The system SHALL classify every non-hit outcome as `Missing`, `Corrupt`, `Truncated`, `ForeignSchema`, `IncompatibleFormatVersion`, `IncompatibleToolVersion`, `KeyMismatch`, `IntegrityMismatch`, `ProjectSetMismatch`, `ArtifactSetMismatch`, `IneligibleBuildInput`, `InputChangedDuringExecution`, `IncompleteOriginalRun`, `Cancelled`, `PathUnsafe`, `SizeExceeded`, or `Disabled`. A miss or reject SHALL fall back to normal verified computation and SHALL NOT itself be reported as an unexplained execution failure. A syntactically valid but structurally incomplete entry, or an exception while accessing its local authentication material, SHALL be a typed `Corrupt` or `PathUnsafe` reject rather than an exception escaping validation.

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
The system SHALL expose equivalent disabled/auto/explicit-path cache semantics through the CLI's `--cache` option and `ArchLinterNet.Testing`'s `ArchitectureValidationBuilder.WithCache()`, both backed by the same `ArchLinterNet.Core.Caching.AnalysisCacheStore`/`AnalysisCachePopulation` implementation and the same shared `ArchLinterNet.Testing.ArchitectureValidationCacheSupport` population/profile-counter logic for both of the Testing host's execution paths (`ArchitectureValidationBuilder.Validate()`'s independent per-mode runs and `ArchitectureValidationSnapshotSession.Evaluate()`'s shared-snapshot runs). A completed, non-cancelled `ArchitectureValidationSnapshotSession.Evaluate` call SHALL populate the cache the same way `ValidateCommandHandler`'s combined-mode execution already does, not merely perform lookups.

#### Scenario: CLI and Testing populate identically
- **WHEN** the CLI and the Testing API validate the same policy with the same `--cache`/`WithCache()` location and configuration
- **THEN** they derive the same `AnalysisCacheKey` digest and observe the same authorization outcome

#### Scenario: A Testing snapshot miss seeds a later snapshot hit
- **WHEN** `ArchitectureValidationSnapshotSession.Evaluate(mode)` completes a non-cancelled miss against a configured cache location
- **THEN** the cache is populated for that mode, and a second session evaluating the same mode against the same policy/inputs observes a `Hit`

### Requirement: Cache population is gated on completed, non-cancelled, eligible runs
The system SHALL capture authorization inputs before contract execution and SHALL populate a cache entry only after a completed, non-cancelled, non-preflight-blocked run whose every discovered project's #406 evaluated-build-input manifest reports `VerifiedCacheEligible`. Immediately before publication, it SHALL recompute those project and artifact manifests and revalidate the captured policy-import and baseline text identities; it SHALL reject with `InputChangedDuringExecution` if any differs from the pre-execution capture and SHALL never construct a post-analysis key/manifest and attach it to an earlier result. `analysis.source_roots`, method-body contracts, and selected `.asmdef` contracts that introduce source/reference inputs not covered by exact manifests SHALL be cache-ineligible until those inputs are fully fingerprinted. Given the current `EvaluatedBuildInputManifestCollector`'s intentional fail-closed behavior (always `CacheIneligible` for real MSBuild evidence), population against real discovered projects SHALL always report `IneligibleBuildInput` today; this is expected and SHALL NOT be worked around by relaxing the eligibility gate.

#### Scenario: A real discovered project is never populated today
- **WHEN** `AnalysisCachePopulation.TryPopulate` is called with a real project discovered from an actual `.csproj`
- **THEN** it returns `IneligibleBuildInput` and no entry file is written

#### Scenario: A preflight-blocked outcome is recorded but never written
- **WHEN** a cache-enabled validation produces a `PreflightBlocked` outcome
- **THEN** population reports `IncompleteOriginalRun`, increments the reject accounting once, and writes no cache entry

#### Scenario: Inputs mutate while contracts execute
- **WHEN** a project manifest or selected PE/PDB/receipt byte digest differs between pre-execution authorization and the immediate pre-publication check
- **THEN** population reports `InputChangedDuringExecution` and writes no cache entry

#### Scenario: Explicit source roots cannot be silently ignored
- **WHEN** a request declares `analysis.source_roots`
- **THEN** lookup and population report `IneligibleBuildInput` until exact source and reference byte manifests are implemented

#### Scenario: A selected asmdef contract cannot be silently reused
- **WHEN** a selected strict or audit `.asmdef` contract would recursively inspect Unity `.asmdef` files
- **THEN** lookup and population report `IneligibleBuildInput` until the effective asmdef file set is fingerprinted

### Requirement: analysis-profile/v1 instrumentation reflects real cache activity
The system SHALL populate `AnalysisProfile.Counters.Cache` with real `Lookups`/`Hits`/`Misses`/`Rejects`/`Writes`/`BytesRead`/`BytesWritten`/`IneligibleUnitCount`/`CorruptionEvents`/`CancelledBeforePublish`/`Mode`/`RejectReasonCounts` whenever `--cache`/`WithCache()` is used with anything other than disabled, setting `Status` to `Active`; `Status` SHALL remain `NotApplicable` (all fields at their zero/default value) when the cache is disabled. `Lookups`/`Hits`/`Misses`/`BytesRead` SHALL be sourced from real `AnalysisCachePopulation.TryLookup` calls made by the cache-hit short-circuit; `IneligibleUnitCount` SHALL include units rejected while preparing that lookup as well as any post-analysis population attempt; and `BytesWritten`/`CorruptionEvents` SHALL be sourced from real `AnalysisCachePopulation`/`AnalysisCacheStore` outcomes. None may remain hardcoded at zero when the underlying activity was non-zero. A `Missing` lookup SHALL increment `Misses` only and SHALL NOT appear in `RejectReasonCounts`. The scalar `Rejects` counter SHALL equal the sum of `RejectReasonCounts`' values — aggregating both population-side rejects and lookup-side (read) rejects consistently — in both the CLI and Testing hosts.

#### Scenario: Cache-enabled run reports Active status
- **WHEN** a `validate --cache auto --profile stdout` run completes
- **THEN** the profile's `Counters.Cache.Status` equals `Active` and `Counters.Cache.Mode` equals `"auto"`

#### Scenario: Cache-disabled run reports NotApplicable
- **WHEN** a `validate --profile stdout` run completes without `--cache`
- **THEN** the profile's `Counters.Cache.Status` equals `NotApplicable`

#### Scenario: Ineligible projects are counted, not just rejected
- **WHEN** a `--cache`-enabled run discovers at least one project whose #406 evaluated-build-input manifest is not `VerifiedCacheEligible`
- **THEN** `Counters.Cache.IneligibleUnitCount` is greater than zero

#### Scenario: A lookup-side reject is reflected in the scalar Rejects total
- **WHEN** a `--cache`-enabled run observes a corrupt cache entry on lookup (a read-side reject) with zero population-side rejects
- **THEN** `Counters.Cache.Rejects` equals the sum of `Counters.Cache.RejectReasonCounts`' values, in both the CLI and Testing hosts

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

### Requirement: Cache entry authenticity is a keyed HMAC tag, not an unkeyed hash
The system SHALL authenticate each `AnalysisCacheEntryV1.ContentDigest` as an HMAC-SHA256 tag keyed by a local, cache-root-scoped secret (`AnalysisCacheHmacKeyStore`), not an unkeyed content hash. The secret SHALL be a 256-bit value generated via a cryptographically secure random number generator on first use of a given cache root, persisted in a separate sibling authentication namespace that is outside `AnalysisCacheLocation.RootPath` and outside its sharded entry tree, and read-or-created idempotently and safely under a concurrent first-use race (every caller observes the exact same key for a given root, never two different keys). The key store SHALL reject a symlink/reparse point at its authentication namespace, root-specific key directory, or key file before it reads, creates, replaces, or removes a key. The stored tag SHALL be compared using a constant-time comparison, never a short-circuiting string/ordinal comparison. A generic CI cache MAY restore the cache root as untrusted optimization data, but it SHALL cache only that root and SHALL NOT include the sibling authentication namespace; restoring a cache archive must therefore never restore the secret that authenticates it. This control defeats a hand-edited/poisoned entry file; it does not defeat an attacker with read/write access to the external key file itself — that residual is an accepted local-trust-boundary limit, stated here rather than left implicit.

#### Scenario: A hand-tampered entry without the real key is rejected
- **WHEN** an entry file's `Outcome.Passed` (or any other field folded into the canonical form) is edited directly on disk by a party that does not know the cache root's HMAC key
- **THEN** `AnalysisCacheStore.TryGet` returns `Reject(IntegrityMismatch)`, never a `Hit`

#### Scenario: A genuine round trip on the same cache root still authenticates
- **WHEN** `AnalysisCacheStore.Put` publishes an entry and `TryGet` is called against the same cache root with the same key and matching project manifests
- **THEN** the result is `Hit`

#### Scenario: Two cache roots derive independent keys
- **WHEN** `AnalysisCacheHmacKeyStore.GetOrCreateKey` is called for two distinct cache root paths
- **THEN** the two returned keys are different, and an entry authenticated under one root's key does not authenticate against the other root

#### Scenario: A symlinked authentication namespace is rejected
- **WHEN** the sibling authentication namespace, root-specific key directory, or `hmac-v1.key` is a symlink or reparse point
- **THEN** the cache operation returns a typed `PathUnsafe` reject and does not access the link target
