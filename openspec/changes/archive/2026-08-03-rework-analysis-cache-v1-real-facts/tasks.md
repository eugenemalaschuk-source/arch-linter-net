## 1. Real reusable facts and closed-set payload converter

- [x] 1.1 Add `AnalysisCacheDiagnosticPayloadConverter` (closed-set, 18 known
      `IArchitectureDiagnosticPayload` types) and register it in `AnalysisCacheJson.Options`.
- [x] 1.2 Replace `AnalysisCacheFactsV1` with `AnalysisCacheOutcomeV1` and `AnalysisCacheOutcomeMapper`
      (`ValidationOutcome` <-> cached shape); update `AnalysisCacheEntryV1`/`AnalysisCacheContentDigest`.

## 2. Cache-hit short-circuit

- [x] 2.1 Thread `AnalysisCacheLocation`/`AnalysisSnapshotCacheContext` through
      `ValidationRequest`/`AnalysisSnapshotRequest`/`ArchitectureValidationApplicationService`.
- [x] 2.2 Add `ArchitectureAnalysisSnapshot.TryEvaluateFromCache`, invoked from `Evaluate(mode)` before
      `EvaluateCore`; add `AnalysisCacheLookupStats`/`ArchitectureAnalysisSnapshotCounters.CacheLookups`.

## 3. Repository root and portable key

- [x] 3.1 Add `ValidationOutcome.RepositoryRoot`, populated by
      `ArchitectureAnalysisSnapshot.EvaluateCore`/`BuildBlockedOutcome`.
- [x] 3.2 CLI/Testing cache population/lookup consume `outcome.RepositoryRoot` instead of
      `Path.GetDirectoryName(policyPath)`.
- [x] 3.3 Rework `AnalysisCacheKey`: repository-relative `PolicyDigest`, new `WorkspaceDigest`, removed
      `RepositoryRootDigest`/`ComputeModeSet`, single `Mode`.

## 4. Symlink hardening and write-side bound

- [x] 4.1 Add `FileSystemContainmentGuard`; wire into `AnalysisCacheStore.ResolveEntryPath`/`TryGet`/
      `Put`/`Inspect`/`Clear` (manual non-symlink-following directory walk).
- [x] 4.2 Enforce `MaxEntryBytes`/`MaxProjectManifests` in `Put` before any file I/O; return
      `PutResult` with `BytesWritten`.

## 5. Per-mode entries and real profile counters

- [x] 5.1 `ExecuteCombinedModes` populates one cache entry per requested mode.
- [x] 5.2 Thread `AnalysisCacheLookupStats`/`AnalysisCachePopulation.Outcome`/`AnalysisCacheStore.PutResult`
      into `AnalysisProfileCacheCounters` (CLI and Testing).

## 6. Schema and verification

- [x] 6.1 Add `schema/0.5.1/analysis-cache.schema.json` and a schema-validation test.
- [x] 6.2 Add regression tests: hit-reconstruction fidelity, per-mode isolation, portable-identity,
      symlink-escape rejection, oversized-write rejection, non-zero profile counters, repository-root
      correctness.
- [x] 6.3 Update `ArchLinterNet.Core.approved.txt` for the intentional public API surface changes.
- [x] 6.4 Run `make fmt`, `make acceptance`, archive this change, `openspec validate --all --strict`.
