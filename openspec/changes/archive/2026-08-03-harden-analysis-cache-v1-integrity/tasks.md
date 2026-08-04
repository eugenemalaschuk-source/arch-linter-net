## 1. Project-set equivalence, root-symlink rejection, reject-counter aggregation

- [x] 1.1 Fix `AnalysisCacheStore.ProjectManifestsMatch` to reject duplicate `ProjectPath` values
      and compare as genuine one-to-one ordered-set equality; add poisoned-duplicate regression.
- [x] 1.2 Reject a cache root that is itself a reparse point in `Inspect`/`Clear`/`TryGet`/`Put`,
      for every `AnalysisCacheMode`, before any I/O; add root-symlink regressions for each operation.
- [x] 1.3 Aggregate population-side and lookup-side rejects into the scalar `Rejects` counter (CLI
      and Testing); populate `CorruptionEvents` in the Testing host via a new shared
      `AnalysisCacheCorruptionClassifier`; add invariant regressions in both hosts.

## 2. Cancellation-safe lookup and complete cache key

- [x] 2.1 Thread the session `CancellationToken` through
      `ArchitectureAnalysisSnapshot.TryEvaluateFromCache`'s `ComputePolicyDigest`/`TryLookup` calls;
      refuse to accept a `Hit` once cancellation is observed; add a regression.
- [x] 2.2 Extend `AnalysisCacheKey` with `PreprocessorSymbolsDigest`, `BaselineDigest`,
      `IncludeAsmdefContracts`, `EnforceUnmatchedIgnoredViolationsPolicy`; update every key-construction
      call site (lookup and population, CLI and Testing) consistently; add one invalidation
      regression per dimension.

## 3. Testing-host cache population parity

- [x] 3.1 Add `ArchitectureValidationCacheSupport` (shared population/profile-counter logic) and
      wire `ArchitectureValidationSnapshotSession.Evaluate` to populate the cache after each
      completed, non-cancelled mode; add a `TestManifestCollectorOverride` test seam on
      `AnalysisCachePopulation` and a miss-then-hit regression.

## 4. Complete cached result envelope

- [x] 4.1 Extend `AnalysisCacheOutcomeV1`/`AnalysisCacheOutcomeMapper` with `ClassificationRoles`,
      `ClassificationPathDeferred`, `CycleFindings`, `CoverageSummaries`,
      `SubtractiveMatcherParticipation`; add `AnalysisCacheClassificationMetadataValueConverter` for
      the closed-set `object` metadata values; update `schema/0.5.1/analysis-cache.schema.json`.
- [x] 4.2 Extend the uncached-vs-cached-hit reconstruction regression to exercise all five fields
      with non-trivial data.

## 5. Keyed HMAC authenticity (finding #1)

- [x] 5.1 Add `AnalysisCacheHmacKeyStore`: CSPRNG-generated 256-bit key, persisted outside the
      sharded entry tree, read-or-created idempotently and safely under concurrent first use.
- [x] 5.2 Replace `AnalysisCacheContentDigest`'s unkeyed hash with an HMAC-SHA256 tag keyed by that
      secret, compared via `CryptographicOperations.FixedTimeEquals`; thread the cache root through
      every call site (`Put`, `Authorize`, `Inspect`'s `BuildSummary`).
- [x] 5.3 Add regressions: hand-tampered entry rejected without the real key; genuine
      Put-then-TryGet round trip still authenticates; two cache roots get independent keys;
      concurrent first-use key-store regressions.

## 6. Verification

- [x] 6.1 Run `make fmt`, `make acceptance NPROC=1`.
- [x] 6.2 Archive this change, `openspec validate --all --strict`.
