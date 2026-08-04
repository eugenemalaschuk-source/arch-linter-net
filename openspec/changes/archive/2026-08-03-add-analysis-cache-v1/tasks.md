## 1. Core caching engine

- [x] 1.1 Add `src/ArchLinterNet.Core/Caching/AnalysisCacheEnvelope.cs` (`SchemaId = "analysis-cache/v1"`, `FormatVersion`, `ToolVersion`, `ProductSchemaVersion`).
- [x] 1.2 Add `AnalysisCacheMode`/`AnalysisCacheOptions`/`AnalysisCacheLocation`/`AnalysisCacheLocationResolver`/`AnalysisCacheLocationRejectedException` (disabled/auto/explicit-path resolution with containment/symlink/root safety checks).
- [x] 1.3 Add `AnalysisCacheKey` (workspace/policy/mode/contract/configuration/TFM/platform/RID reuse-authorization key) and `AnalysisCacheProjectManifest` (wraps `EvaluatedBuildInputManifestV1`).
- [x] 1.4 Add `AnalysisCacheFactsV1` (deterministic counts only, never polymorphic finding detail) and `AnalysisCacheEntryV1`/`AnalysisCacheEntryCompletionStatus`/`AnalysisCacheContentDigest`.
- [x] 1.5 Add `AnalysisCacheRejectReason`/`AnalysisCacheLookupOutcome`/`AnalysisCacheLookupResult`.
- [x] 1.6 Add `AnalysisCacheStore` (`TryGet`/`Put`/`Inspect`/`Clear`): bounded reads, full authorization chain, atomic staged writes with cancellation-before-publish, deterministic safe `Inspect`/`Clear`.
- [x] 1.7 Add `AnalysisCachePopulation` (shared CLI/Testing population + lookup helper over `EvaluatedBuildInputManifestCollector` + `AnalysisCacheStore`).

## 2. CLI integration

- [x] 2.1 Add `--cache <disabled|auto|path>` to `ValidateCommandDefinition`/`ValidateCommandOptions` (default disabled).
- [x] 2.2 Pre-validate the resolved cache location before analysis starts (`PreValidateCacheDestination`), mirroring `--profile`'s pre-validation.
- [x] 2.3 Populate the cache after a completed, non-cancelled single-mode/combined-mode run via `ValidateCommandHandler.Cache.cs`, gated on #406 eligibility; fail-safe on I/O errors (never turn a successful validation into an execution error).
- [x] 2.4 Add `cache inspect`/`cache clear` as a new `CacheCommandModule`/`CacheCommandDefinition`/`CacheCommandHandler`.
- [x] 2.5 Thread real cache counters into `AnalysisProfileBuildOptions.Cache` for `--profile` output.

## 3. Testing API mirror

- [x] 3.1 Add `ArchitectureValidationBuilder.WithCache(AnalysisCacheOptions)`, sharing `AnalysisCachePopulation`/`AnalysisCacheStore` with the CLI.
- [x] 3.2 Thread cache counters into `ArchitectureValidationResult.Profile` when both `WithProfile()` and `WithCache()` are used.

## 4. Instrumentation

- [x] 4.1 Extend `AnalysisProfileCacheCounters` with `Misses`/`Rejects`/`Writes`/`BytesRead`/`BytesWritten`/`IneligibleUnitCount`/`CorruptionEvents`/`CancelledBeforePublish`/`Mode`/`RejectReasonCounts`, keeping `Status`/`Lookups`/`Hits` unrenamed.
- [x] 4.2 Add `AnalysisProfileReservedFieldStatus.Active`.
- [x] 4.3 Update `schema/0.5.1/analysis-profile.schema.json`'s `Cache` section for the new fields/enum value.
- [x] 4.4 Update `docs/internal/analysis-profile-dictionary.md`'s `Cache` row.

## 5. Tests

- [x] 5.1 `AnalysisCacheStoreTests`: miss-when-missing, hit, ineligible-manifest reject, project-set-mismatch reject, key-mismatch, corrupt/truncated/foreign-schema reject, cancelled-before-publish leaves no entry, deterministic `Inspect` without absolute paths, `Clear` removes entries and refuses filesystem roots.
- [x] 5.2 `AnalysisCacheLocationResolverTests`: disabled → null, auto resolves under product/schema-version path, explicit path canonicalized, empty/root/existing-file/symlink rejected.
- [x] 5.3 `AnalysisCacheKeyTests`: digest stability, portable repository-root digest, order-independent contract-id/mode-set digests.
- [x] 5.4 `AnalysisCachePopulationTests`: real project is ineligible today (documents #406's intentional state), disabled reports `Disabled`, no discovered projects is ineligible.
- [x] 5.5 CLI: `ValidateCommandHandlerProfileTests` additions for `--cache` omitted/auto/unsafe-path; `CacheCommandHandlerTests` for inspect/clear/help/unsafe-path/missing-destination.
- [x] 5.6 Core: `AnalysisCacheTestingApiIntegrationTests` for `WithCache()` wiring and profile counters.
- [x] 5.7 Fix pre-existing tests that enumerate CLI subcommands (`CliArchitectureTests`) and exhaustive Core namespace lists (`LayerTemplateContractTests`) to include the new `cache` command / `ArchLinterNet.Core.Caching` namespace.
- [x] 5.8 Regenerate `tests/ArchLinterNet.Core.Tests/ApprovedApi/ArchLinterNet.Core.approved.txt` for the new public API surface.

## 6. Spec sync and archive

- [x] 6.1 Compare implementation against this proposal/design; adjust the delta spec to describe only what was actually built (including the deferred pipeline short-circuit).
- [x] 6.2 Run `openspec validate --all`.
- [x] 6.3 Run `openspec archive add-analysis-cache-v1`.
