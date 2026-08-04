## Why

PR #426 (issue #365) shipped a real, safe `analysis-cache/v1` storage/authorization engine, but a
maintainer review of commit 64242e2 found the change should not merge as-is: it claims to close
#365 while explicitly deferring the cache-hit short-circuit, and it caches a bare `Passed` boolean
plus aggregate counts rather than reusable facts — the issue's own acceptance criteria (reconstruct
byte-identical findings/identity/ordering/exit-category on a hit) cannot be met from that shape. Six
more P1 findings (wrong repository root, symlink-escape in path containment, combined-mode facts
collapsed to `outcomesByMode[0]`, a non-portable cache key hashing absolute paths, profile counters
mostly hardcoded to zero, and a write side with no size bound) and one P2 (missing JSON Schema) round
out the review. This change fixes all seven for real and implements the short-circuit the original
change explicitly deferred.

## What Changes

- Replace `AnalysisCacheFactsV1` (bool + counts) with `AnalysisCacheOutcomeV1`: a real per-mode
  reusable fact set carrying `Violations` (via a closed-set `IArchitectureDiagnosticPayload`
  converter enumerating all 18 concrete payload types), `Cycles`, `UnmatchedIgnoredViolations`,
  `PolicyConsistencyFindings`, `ClassificationConflicts`/`ClassificationMetadataFailures`, and
  `Passed` — enough to reconstruct a `ValidationOutcome` with byte-identical findings, ordering, and
  exit category on a hit.
- Add a real cache-hit short-circuit: `ArchitectureAnalysisSnapshot.Evaluate(mode)` attempts a cache
  lookup before running contract execution/coverage/classification/policy-consistency checks for
  that mode, reconstructing the outcome from a hit via `AnalysisCacheOutcomeMapper` instead. Policy
  composition, project discovery, and assembly loading still always run (needed to prove per-project
  #406 eligibility and shared across every requested mode), so this skips the per-mode contract
  execution work, not project/assembly setup.
- Thread the authoritative `ArchitectureAnalysisSnapshot.RepositoryRoot` through `ValidationOutcome`
  and use it for cache population/lookup in both CLI and Testing, instead of re-deriving a
  (sometimes wrong) root from `Path.GetDirectoryName(policyPath)`.
- Make `AnalysisCacheStore`'s path containment reparse-point-aware (mirroring
  `EvaluatedBuildInputManifestCollector`'s own symlink defense via a new shared
  `FileSystemContainmentGuard`), and make `Inspect`/`Clear` walk directories without following
  symlinked subdirectories.
- Make `AnalysisCacheKey` portable: `PolicyDigest` hashes repository-relative paths, never absolute
  ones; the non-portable `RepositoryRootDigest` is removed; a separate `WorkspaceDigest` (also
  repository-relative) provides workspace/trust-domain binding as an explicit, independent control.
- Cache one entry per requested mode (`AnalysisCacheKey.Mode` is a single mode, never a joined set);
  `ValidateCommandHandler.Execution.cs`'s combined-mode path now populates once per `(mode, outcome)`
  pair instead of only `outcomesByMode[0]`.
- Thread real `Lookups`/`Hits`/`Misses`/`BytesRead`/`BytesWritten`/`IneligibleUnitCount`/
  `CorruptionEvents`/`CancelledBeforePublish` into `AnalysisProfileCacheCounters` from
  `AnalysisCacheLookupStats` and `AnalysisCachePopulation.Outcome`/`AnalysisCacheStore.PutResult`.
- Enforce `MaxEntryBytes` (and a manifest-count bound) on `AnalysisCacheStore.Put` before any write,
  not only on `TryGet`.
- Add `schema/0.5.1/analysis-cache.schema.json` and a schema-validation test mirroring
  `analysis-profile`'s pattern.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-cache`: real reusable fact envelope, cache-hit short-circuit, portable key, per-mode
  entries, symlink-hardened store, write-side size bound, real instrumentation, dedicated schema.

## Impact

`ArchLinterNet.Core.Caching` (new/changed types throughout), `ArchLinterNet.Core.Validation`
(`ValidationOutcome`, `ArchitectureAnalysisSnapshot`, `ArchitectureValidationApplicationService`,
request types), `ArchLinterNet.Core.BuildState` (`BuildStateCanonicalHasher`,
`FileSystemContainmentGuard`), `ArchLinterNet.Cli.Commands.Validate` (`ValidateCommandHandler.Cache.cs`/
`.Execution.cs`), `ArchLinterNet.Testing.ArchitectureValidationBuilder`, `schema/0.5.1/`, and cache
test suites in both `ArchLinterNet.Core.Tests` and `ArchLinterNet.Cli.Tests`.
