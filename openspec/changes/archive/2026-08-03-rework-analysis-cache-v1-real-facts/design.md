## Context

The prior change (`openspec/changes/archive/2026-08-03-add-analysis-cache-v1/design.md`) explicitly
deferred two things and named the exact reasons: caching full finding detail needed a closed-set
`IArchitectureDiagnosticPayload` JSON converter that didn't exist yet, and wiring a cache hit into
`ArchitectureValidationApplicationService`/`ArchitectureAnalysisSnapshot` needed a seam that didn't
exist yet. A maintainer review of the resulting PR (#426, commit 64242e2) confirmed both gaps are
real defects against the issue's own acceptance criteria, not acceptable scope narrowing, and named
five more concrete defects (repository root, symlink containment, combined-mode facts, cache-key
portability, and profile counters) plus a missing JSON Schema. This change builds the converter, the
seam, and fixes the other five findings.

## Decisions

**Closed-set payload converter, not per-payload DTOs.** `IArchitectureDiagnosticPayload`'s 18
concrete records (`FrameworkReferenceAllowOnlyPayload` … `PublicApiSurfacePayload`, all in
`ArchLinterNet.Core.Model`) have only primitive/string/enum/simple-collection/`IReadOnlyDictionary
<string, object>` fields — no further polymorphism. Rather than mirroring `ValidationOutcome`'s
entire object graph into a parallel DTO universe, `AnalysisCacheOutcomeV1` caches the *real* Core
model types directly (`ArchitectureViolation`, `ArchitectureUnmatchedIgnoredViolation`,
`PolicyConsistencyDiagnostic`, `ArchitectureClassificationConflict`/`MetadataFailure`) and adds
exactly one new converter, `AnalysisCacheDiagnosticPayloadConverter : JsonConverter
<IArchitectureDiagnosticPayload>`, registered only in `AnalysisCacheJson.Options`. It writes
`{"$kind": "<TypeName>", "value": {...}}` and reads back via an explicit `switch` over 18
`nameof(...)` cases — never `Type.GetType`, never an assembly-qualified name, never
`TypeNameHandling`. An unrecognized `$kind` throws `JsonException`, surfaced by `AnalysisCacheStore`
as `Corrupt`. This is the literal "explicit closed-set converter enumerating all ... concrete types"
the review asked for.
*Alternative considered*: `[JsonDerivedType]`/`JsonPolymorphic` attributes on the interface itself.
Rejected to avoid adding JSON-serialization concerns to `ArchLinterNet.Core.Model`, which today has
no `System.Text.Json` dependency; the hand-written converter keeps that decision local to
`ArchLinterNet.Core.Caching`.

**Cache boundary, v2: real findings, still not everything.** `AnalysisCacheOutcomeV1` caches
`Passed`, `Violations`, `Cycles`, `UnmatchedIgnoredViolations`, `PolicyConsistencyFindings`,
`ClassificationConflicts`/`ClassificationMetadataFailures` — the fields that determine reported
findings, their ordering, baseline identity, and exit category. It deliberately does *not* cache
`ArchitectureCoverageSummary`, `ArchitectureClassificationRoleFact`,
`ArchitectureClassificationPathDeferredNotice`, `ArchitectureSourceExpansionInventory`,
`ArchitectureSubtractiveMatcherParticipation`, or `BuildStatePreflightDiagnostic` — supplementary
explain/coverage-report detail, not findings identity/ordering/exit-category. A reconstructed
`ValidationOutcome` restores these as empty/default and always `PreflightBlocked = false` (population
never persists an entry for a run whose project set isn't proven current). This is a disclosed scope
line, not a silent gap — widening it later is additive (bump `AnalysisCacheEnvelope.FormatVersion`).

**Content digest over cached findings: hash the canonical JSON, not a hand-canonicalized string.**
The prior `AnalysisCacheFactsV1` had 11 scalar fields, cheap to canonicalize by hand into an ordinal
string join. `AnalysisCacheOutcomeV1` carries deeply nested collections of records. Hand-canonicalizing
every nested field would duplicate `AnalysisCacheDiagnosticPayloadConverter`'s own closed-set
knowledge for no integrity benefit. `AnalysisCacheContentDigest` instead hashes
`JsonSerializer.SerializeToUtf8Bytes(entry.Outcome, AnalysisCacheJson.Options)` — the same
`JsonSerializerOptions` used to persist the entry, so serialization is deterministic (fixed declared-
property order, list order preserved) and any bit of the outcome changing changes the digest,
verified byte-for-byte on every read exactly like the rest of the envelope.

**The short-circuit seam: `ArchitectureAnalysisSnapshot.Evaluate`, not
`ArchitectureRunnerSetupService`.** The truly expensive, mode-specific work — contract execution
(including source scanning), coverage computation, classification checks, policy-consistency
checks — happens inside `ArchitectureAnalysisSnapshot.EvaluateCore`, called once per requested mode
from `Evaluate(mode)`. `Evaluate` already memoizes per mode (`_evaluatedModes`); this change adds one
more check before that memoization: if an `AnalysisSnapshotCacheContext` was supplied, attempt
`AnalysisCachePopulation.TryLookup` (which itself recomputes per-project #406 manifests against the
snapshot's already-known discovered projects) and, on `Hit`, reconstruct via
`AnalysisCacheOutcomeMapper.FromCacheOutcome` instead of calling `EvaluateCore`.
*Alternative considered*: splitting `ArchitectureRunnerSetupService.BuildRunnerCore` so a cache check
could sit between project discovery and assembly loading/session construction, skipping those too.
Rejected for this change: discovery and assembly resolution are currently one non-separable call
(`BuildRunnerCore`), and splitting them is itself a nontrivial, independently reviewable change to
`Core.Execution` — the same class of risk the original change correctly avoided taking on blind.
Given `EvaluatedBuildInputManifestCollector` is fail-closed until #406 anyway (see below), a hit can
only ever occur *after* project discovery has already run in practice, so skipping the mode-specific
evaluation work is the seam with the best value-to-risk ratio available today.

**Portable key: repository-relative content, workspace digest as a separate control.**
`AnalysisCacheKey` no longer hashes `Path.GetFullPath(repositoryRoot)` (removed
`RepositoryRootDigest` entirely) or absolute policy paths. `ComputePolicyDigest` now takes
`repositoryRoot` and hashes each policy file's content joined with its path *relative* to
`repositoryRoot` (via a newly-public `BuildStateCanonicalHasher.ToRepositoryRelativePath`, promoted
from a private helper already used for the exact same normalization inside
`ComputeBuildInputFingerprint`). A new `WorkspaceDigest` — a repository-relative, sorted digest of
the discovered project paths — is a second, independent input to the key: it distinguishes which
workspace produced an entry without depending on any absolute checkout path, matching the review's
"add trust-domain authorization as a separate control" rather than folding it into portability.
`ComputeModeSet`/joined mode-set strings are removed; `AnalysisCacheKey.Mode`/`NormalizeMode` carry
exactly one mode.

**Per-mode entries, not per-mode-set.** `AnalysisCacheEntryV1.Mode` (a single string) plus
`AnalysisCacheKey.Mode` mean a combined `strict,audit` CLI request now builds and populates one key
per requested mode from that mode's own `ValidationOutcome`, never `outcomesByMode[0]`'s outcome
under a joined key. `ValidateCommandHandler.Execution.cs`'s `ExecuteCombinedModes` loops
`outcomesByMode` and calls `TryPopulateCache` once per pair.

**Symlink hardening: a shared, explicit guard, reused only where safe.** `FileSystemContainmentGuard`
(new, `Core.BuildState`) provides `IsContained`/`HasReparsePointAncestor`/`IsReparsePoint`, mirroring
`EvaluatedBuildInputManifestCollector`'s existing private symlink-ancestor walk. `AnalysisCacheStore`
now calls it before every read/write (`ResolveEntryPath`, `TryGet`, `Put`) and walks
`Inspect`/`Clear` directory recursion manually instead of `Directory.EnumerateFiles(...,
SearchOption.AllDirectories)`, which would otherwise follow a symlinked subdirectory. The walk never
climbs above the cache root even when the root itself doesn't exist yet (the common first-write
case) — climbing past a non-existent root would start inspecting the root's own *parent* ancestors
(e.g. a platform temp-directory symlink such as macOS's `/var` → `/private/var`), which are outside
this guard's threat model and would otherwise produce false-positive rejections.
`EvaluatedBuildInputManifestCollector`'s own existing private symlink-check methods are left
untouched (not redirected to the new shared guard) — their exact fail-closed edge-case semantics
(e.g. returning `true`/unsafe when the walk exhausts without reaching root) are already covered by
existing tests, and this change does not want to risk altering that behavior as a side effect of a
refactor unrelated to its own scope.

**Write-side size/count bound.** `AnalysisCacheStore.Put` now serializes to bytes, checks
`bytes.LongLength` against `MaxEntryBytes` (and `projectManifests.Count` against a new
`MaxProjectManifests`), and returns `PutResult.Rejected(SizeExceeded)` before any file I/O when
exceeded — mirroring the existing read-side bound exactly, so a write that would only ever be
rejected by every subsequent `TryGet` never reaches the filesystem.

**Real profile counters.** `AnalysisCacheLookupStats` (new) aggregates every
`AnalysisCachePopulation.TryLookup` result observed by one `ArchitectureAnalysisSnapshot` (exposed
via `ArchitectureAnalysisSnapshotCounters.CacheLookups`); `AnalysisCacheStore.PutResult`/
`AnalysisCachePopulation.Outcome` carry `BytesWritten`/`IneligibleProjectCount`. CLI/Testing combine
both into `AnalysisProfileCacheCounters`, deriving `CorruptionEvents` from the
`Corrupt`/`Truncated`/`IntegrityMismatch`/`ForeignSchema` reject-reason counts.

## Risks / Trade-offs

- [Risk] `EvaluatedBuildInputManifestCollector` still always reports `CacheIneligible` for real
  MSBuild evidence (unchanged, #406's own future scope) — so this change's cache-hit short-circuit,
  while fully wired into the live pipeline, cannot yet be observed producing a `Hit` through the full
  CLI/Testing pipeline against a genuinely discovered project in this repository's own environment
  today. → Mitigation: proven at the `AnalysisCacheStore`/`AnalysisCacheOutcomeMapper` level instead
  (hand-built `VerifiedCacheEligible` manifests), which is exactly how the prior change's own
  `TryPopulate_RealProject_IsIneligibleBuildInputToday` test already worked around the identical
  constraint; the seam requires no further changes once #406 ships a real eligible collector.
- [Risk] The cached-outcome scope line (no coverage summaries/classification roles/source expansion)
  means a hit's `ValidationOutcome` is not a 100% complete substitute for every field a report
  renderer might read. → Mitigation: documented explicitly above and in `AnalysisCacheOutcomeV1`'s
  own doc comment; none of the omitted fields affect findings, ordering, identity, or exit category.
- [Risk] `EvaluatedBuildInputManifestCollector`'s own symlink-check methods were left as private
  duplicates rather than refactored onto the new shared `FileSystemContainmentGuard`. → Mitigation:
  deliberate, to avoid changing tested, working, unrelated code as a side effect; `AnalysisCacheStore`
  itself is fully covered by the shared guard.

## Migration Plan

Additive/corrective, no public CLI flag or exit-code changes. `AnalysisCacheEntryV1`'s on-disk shape
changes (`Facts` → `Outcome`, new `Mode` field) — `FormatVersion` is unchanged (still `1`) because no
previously-populated real entry exists to migrate: `EvaluatedBuildInputManifestCollector`'s fail-closed
behavior means no entry has ever actually been written against a real project in any environment
running this tool. `AnalysisCacheKey.Digest`'s inputs also change (removed `RepositoryRootDigest`,
added `WorkspaceDigest`), which changes the key/file digest for any hand-populated entry from before
this change — again moot given no real entry has ever existed.

## Open Questions

None outstanding; the short-circuit's remaining depth (splitting project discovery from assembly
loading to skip more pipeline work on a hit) is explicitly left to a future change once #406 makes a
`Hit` observable in practice.
