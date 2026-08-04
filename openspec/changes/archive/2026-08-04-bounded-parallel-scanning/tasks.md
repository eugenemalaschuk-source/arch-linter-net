## 1. Max-parallelism option surface

- [x] 1.1 Add a max-parallelism resolution/validation helper in `ArchLinterNet.Core` (default `max(1, min(Environment.ProcessorCount, 4))`, reject values `<= 0`).
- [x] 1.2 Add `CancellationToken`-style last-parameter `int? MaxParallelism` to `ValidationRequest`/`AnalysisSnapshotRequest`, threaded into `ArchitectureAnalysisContext`.
- [x] 1.3 Add `--max-parallelism <n>` to `ValidateCommandDefinition`/`ValidateCommandHandler`, following the `--cache`/`--profile` pattern.
- [x] 1.4 Add `ArchitectureValidationBuilder.WithMaxParallelism(int? maxDegreeOfParallelism)` to the Testing API.

## 2. Bounded parallel partition/merge helper

- [x] 2.1 Implement a small internal `BoundedParallelPartitionRunner`-style helper in `Core/Execution/` that partitions by index, runs partitions concurrently bounded by the resolved degree, merges results back in original index order, and unwraps `AggregateException` cancellation to a direct `OperationCanceledException`.
- [x] 2.2 Implement the small-work-set/`max-parallelism 1` sequential-path skip (no `Parallel`/`Task` allocation) inside the helper.
- [x] 2.3 Add `Interlocked`-based scheduled/completed/observed-max-concurrency/merge-count instrumentation to the helper, exposed for `AnalysisProfileConcurrencyCounters` population.

## 3. Parallelize type loading

- [x] 3.1 Rework `ArchitectureTypeIndex.LoadAllTypes()` to partition per target assembly through the new helper and merge in original assembly order.
- [x] 3.2 Verify `Lazy<Type[]>` still guarantees at-most-once materialization per snapshot with the parallel path.

## 4. Parallelize source-file fact index materialization

- [x] 4.1 Rework `ArchitectureSourceFileFactIndex.RunReflectionPass` to partition per pre-sorted assembly through the helper, merging in sorted-assembly order before `BuildFacts`' existing sort.
- [x] 4.2 Rework `ArchitectureSourceFileFactIndex.RunSourceScan` to partition per source root through the helper, merging in source-root declaration order before `ResolveSourceInfo`/`SortFactsAndAmbiguities`.
- [x] 4.3 Confirm `SortFactsAndAmbiguities`'s final sort still makes output order independent of merge/partition scheduling.

## 5. analysis-profile Concurrency counters

- [x] 5.1 Extend `AnalysisProfileConcurrencyCounters` with scheduled/completed/observed-max-concurrency/merge-count fields alongside the existing `Status`/`Workers`.
- [x] 5.2 Wire `AnalysisProfileBuilder.Build` to populate `Counters.Concurrency` from the helper's recorded instrumentation, `Active` only when at least one phase actually ran parallel.
- [x] 5.3 Update `docs/internal/analysis-profile-dictionary.md` (or equivalent) to document the new Concurrency fields.

## 6. Tests

- [x] 6.1 Add a test proving `--max-parallelism 1` and `--max-parallelism 4` (or higher) produce byte-identical canonical findings/ordering for a multi-assembly/multi-file fixture.
- [x] 6.2 Add a test proving repeated parallel runs are byte-stable (deterministic JSON/SARIF output).
- [x] 6.3 Add a test forcing a small threshold to exercise the parallel path even on a small fixture (internal test-only construction option), verifying merge-order correctness independent of completion order.
- [x] 6.4 Add a cancellation test for `ArchitectureTypeIndex.LoadAllTypes()` mid-parallel-scan: `OperationCanceledException` raised directly, no partial result exposed.
- [x] 6.5 Add a cancellation test for `ArchitectureSourceFileFactIndex.BuildData()` mid-parallel-scan: same guarantee.
- [x] 6.6 Add a test proving cache-enabled/cache-disabled runs remain equivalent under bounded parallel scanning (cache hit skips parallel scanning entirely; cache miss uses it).
- [x] 6.7 Add a test proving `--max-parallelism 0`/negative values are rejected before scanning begins, for both CLI and Testing API.
- [x] 6.8 Add a test proving `Counters.Concurrency` reports `Active` with real counts on a parallel-eligible run and `NotApplicable`/all-zero on a sequential-mode run.
- [x] 6.9 Add/extend a CLI acceptance test exercising `--max-parallelism` end to end.

## 7. Spec sync and validation

- [x] 7.1 Run `rtk make fmt` and `rtk make acceptance`; fix any issue-related failures.
- [x] 7.2 Reconcile `openspec/changes/bounded-parallel-scanning/specs/**` against the actual implemented behavior.
- [x] 7.3 Run `openspec validate --all`.
- [x] 7.4 Run `openspec archive bounded-parallel-scanning`.
