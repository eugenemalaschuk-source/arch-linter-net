## Why

PR #426 (issue #365)'s third review round on commit 06dba28 found 8 new problems (7 P1, 1 P2) —
all exposed by the cache-hit short-circuit itself, which did not exist when the earlier rounds
reviewed an inert cache. The deepest one (finding #1): `AnalysisCacheContentDigest` was an unkeyed
SHA-256 hash, so a poisoned or restored cache entry could set `Passed = true` with empty findings,
recompute a matching digest, pass `Authorize`, and make `Evaluate` skip all contracts — the closed-set
payload converter prevents arbitrary CLR construction but never prevented semantic poisoning. The
repo owner chose the thorough fix for this: real cryptographic tamper-evidence (keyed HMAC), not
just documenting the gap as a non-goal. The other 7 findings are real, independently confirmed bugs:
a duplicate-manifest bypass in project-set authorization, a missing `CycleFindings` field (and four
siblings) in the cached outcome envelope, missing cache-key dimensions, a cancellation gap in the
lookup path, an un-guarded cache-root symlink, and reject-counter aggregation bugs in both hosts.

## What Changes

- Replace the unkeyed `AnalysisCacheContentDigest` hash with an HMAC-SHA256 tag keyed by a new
  `AnalysisCacheHmacKeyStore` secret: a 256-bit CSPRNG key generated once per cache root, persisted
  outside the sharded entry tree (`<root>/.keys/hmac-v1.key`), read-or-created idempotently under
  concurrent first use, and compared via `CryptographicOperations.FixedTimeEquals`. This is a local
  trust-boundary control (documented as such, not oversold): it defeats hand-edited entry files, not
  an attacker with full read/write access to the key file itself.
- Fix `AnalysisCacheStore.ProjectManifestsMatch` to reject duplicate `ProjectPath` values on either
  side and compare as genuine ordered-set equality, closing a bypass where a forged stored list with
  a duplicate path could still authorize against a distinct current project set.
- Reject the cache root itself being a symlink/reparse-point directory in every
  `AnalysisCacheStore` operation and every `AnalysisCacheMode` (previously only validated for
  `ExplicitPath` at resolution time, never for `Auto`, and never for the root itself vs. nested
  shard paths).
- Extend `AnalysisCacheOutcomeV1`/`AnalysisCacheOutcomeMapper` with the five previously-omitted
  result-bearing `ValidationOutcome` fields: `ClassificationRoles`, `ClassificationPathDeferred`,
  `CycleFindings`, `CoverageSummaries`, `SubtractiveMatcherParticipation`.
- Fold every remaining result-affecting `AnalysisSnapshotRequest`/`ValidationRequest` dimension into
  `AnalysisCacheKey`: `PreprocessorSymbols` (order-independent digest), baseline content digest
  (never the baseline's path), `IncludeAsmdefContracts`, `EnforceUnmatchedIgnoredViolationsPolicy`.
- Thread the session `CancellationToken` through `ArchitectureAnalysisSnapshot.TryEvaluateFromCache`
  (`ComputePolicyDigest`, `TryLookup`) and refuse to accept a `Hit` once cancellation is observed.
- Wire `ArchitectureValidationSnapshotSession.Evaluate` to populate the cache after each completed,
  non-cancelled mode (it previously only ever performed lookups), via a new shared
  `ArchitectureValidationCacheSupport` helper reused by `ArchitectureValidationBuilder.Validate()`.
- Aggregate population-side and lookup-side reject counts consistently into the scalar `Rejects`
  counter (previously population-only) in both CLI and Testing hosts, and populate
  `CorruptionEvents` in the Testing host (previously left at zero).

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-cache`: keyed HMAC authenticity tag, genuine project-set equivalence, root-symlink
  rejection in every mode, a complete cached result envelope, every result-affecting key dimension,
  cancellation-safe lookup, Testing-host cache population parity with the CLI, and consistent
  reject-counter aggregation.

## Impact

`ArchLinterNet.Core.Caching` (`AnalysisCacheContentDigest`, new `AnalysisCacheHmacKeyStore`, new
`AnalysisCacheCorruptionClassifier`, new `AnalysisCacheClassificationMetadataValueConverter`,
`AnalysisCacheStore`, `AnalysisCacheKey`, `AnalysisCacheOutcomeV1`/`AnalysisCacheOutcomeMapper`,
`AnalysisCacheJson`), `ArchLinterNet.Core.Validation` (`ArchitectureAnalysisSnapshot`,
`AnalysisSnapshotCacheContext`, `ArchitectureValidationApplicationService`),
`ArchLinterNet.Cli.Commands.Validate.ValidateCommandHandler.Cache.cs`,
`ArchLinterNet.Testing` (new `ArchitectureValidationCacheSupport`, `ArchitectureValidationBuilder`,
`ArchitectureValidationSnapshotSession`), `schema/0.5.1/analysis-cache.schema.json`, and cache test
suites in `ArchLinterNet.Core.Tests`/`ArchLinterNet.Cli.Tests`.
