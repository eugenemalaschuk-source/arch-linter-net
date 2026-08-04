## Context

`ArchLinterNet.Core`'s validation seam builds one `ArchitectureAnalysisSnapshot` per run (analysis-snapshot) whose `ArchitectureAnalysisSession` (analysis-session-indexes) lazily constructs a `TypeIndex`, `RoleIndex`, and `SourceFileFactIndex` on first access. Two of those — `ArchitectureTypeIndex.LoadAllTypes()` and `ArchitectureSourceFileFactIndex.BuildData()` — do read-only, per-assembly/per-source-root work today via plain sequential `foreach` loops, each backed by a `Lazy<T>` so the work runs at most once per snapshot regardless of parallelism.

Everything downstream of those two indexes — contract-family execution (`ArchitectureContractExecutor.Execute`) — is explicitly out of scope. It assigns canonical finding identity from `ArchitectureAnalysisSession._findingIdentityCandidates`, a single shared list that grows as contracts run; each contract captures `session.FindingIdentityCursor` (`= _findingIdentityCandidates.Count`) immediately before it executes and later slices `_findingIdentityCandidates[cursor..]` to attribute candidates to itself. That mechanism is correct only if contracts run one at a time and candidates are appended in a stable order — parallelizing it would misattribute findings between contracts. Session-level mutable lists reached during scanning (`_unmatchedIgnoredViolations`, `_baselineCandidates`, `_registeredContextualConsumers` in `ArchitectureAnalysisSession`, mutated through `ArchitectureContractExecutionContext.IsIgnored`) are populated during contract execution, not during `LoadAllTypes()`/`BuildData()`, so they are unaffected by parallelizing those two indexes.

`AnalysisProfileConcurrencyCounters` already exists (added by #374) specifically reserved for this issue, currently hardcoded to `Status = NotApplicable`/`Workers = 0` because `AnalysisProfileBuilder.Build` never assigns it.

## Goals / Non-Goals

**Goals:**
- Bound and make configurable the parallelism used for `ArchitectureTypeIndex.LoadAllTypes()` and `ArchitectureSourceFileFactIndex.BuildData()`'s reflection and source-enumeration passes.
- Guarantee the exact same output content and order at every supported parallelism level (1..N), so this is purely a scheduling/latency change, never a behavior change.
- Make `--max-parallelism 1` a fully supported, literally-sequential mode (no `Parallel`/`Task` overhead at all).
- Populate `Counters.Concurrency` with real, verifiable scheduling evidence.
- Preserve cancellation semantics: no partial parallel work is ever merged into a published result.

**Non-Goals:**
- Parallelizing contract-family execution, coverage/classification post-processing, or any phase that touches `ArchitectureAnalysisSession`'s shared mutable candidate/consumer state.
- Parallelizing cache lookup/population (#365 already gates scanning behind a per-mode cache check; this change does not alter that ordering).
- Distributed or unbounded execution.
- Requiring parallelism for correctness — sequential (`--max-parallelism 1`) must remain a first-class, fully supported mode, not a deprecated fallback.
- Changing `ArchitectureAnalysisSnapshot.Evaluate`'s existing `lock (_gate)` per-mode serialization — modes still evaluate one at a time; this change is entirely inside what happens the first time a given mode's evaluation reaches a lazy index inside `EvaluateCore`.

## Decisions

### Partition-then-deterministic-merge, not concurrent-append
Both target phases follow the same shape: partition the work by a stable, pre-existing index (assembly position in `LoadAllTypes`; assembly position for the reflection pass and source-root declaration position for the source-enumeration pass in `BuildData`), compute each partition independently and concurrently into its own local result, then merge the partitions back together strictly in that original index order on the calling thread after all partitions complete. This is chosen over having concurrent workers append directly into a shared `List`/`Dictionary` (which would require synchronization on every append and make output order dependent on completion order, not partition order) or over `ConcurrentBag`/`ConcurrentDictionary` (same completion-order-dependence problem, plus `ConcurrentDictionary` does not preserve insertion order). Partition-then-merge is the only shape that makes output byte-identical to the sequential baseline independent of scheduling, at the cost of holding all partition results in memory until the merge step — acceptable given these are the same objects (`Type[]`, small per-assembly fact lists) the sequential path already builds in full before returning.

### A single small `BoundedParallelPartitionRunner` helper, not `Parallel.ForEach`/`Parallel.For` directly
A minimal internal helper (in `Core/Execution/`) wraps `Parallel.For(0, count, options, ...)` writing into a preallocated `T?[]` indexed by partition position, with `ParallelOptions.MaxDegreeOfParallelism` and `.CancellationToken` set from the resolved max-parallelism and the session's token. Both `LoadAllTypes` and `BuildData` call this helper with their own per-partition delegate. A shared helper (rather than duplicating the partition/merge boilerplate in both files) keeps the "index-preserving merge" invariant enforced in one place and is where `Counters.Concurrency`'s scheduled/completed/observed-max-concurrency/merge counters are recorded via `Interlocked` operations, so both call sites get identical instrumentation for free. It is an **instance**, not a static utility: `ArchitectureTypeIndex` and `ArchitectureSourceFileFactIndex` each hold one (constructed by default, overridable through their internal constructors), the same way they already accept an overridable `AnalysisSessionProfilingCounters?`. A static class would permanently couple every caller to `Parallel.For`'s real thread scheduling; an instance lets tests substitute a deterministic fake instead of relying on `Thread.Sleep`-based races to exercise ordering/cancellation behavior.

### Small-input threshold skips the parallel path entirely
Below a fixed threshold (`BoundedParallelPartitionRunner.DefaultParallelEligibilityThreshold` — 4 partitions, i.e. 1–3 assemblies/source roots), or when the resolved max-parallelism is `1`, the helper runs the existing sequential loop body directly on the calling thread with no `Parallel.For`, no `ParallelOptions` allocation, and no thread-pool scheduling. This satisfies "small repositories avoid disproportionate parallel setup overhead" and "`--max-parallelism 1` ... is a first-class supported sequential mode" as literal code-path guarantees, not just a degenerate case of the parallel path with `MaxDegreeOfParallelism = 1` (which would still pay `Parallel.For`'s partitioning/task overhead).

### Default resolution: `max(1, min(Environment.ProcessorCount, 4))`, validated like other options
`--max-parallelism`/`WithMaxParallelism(int?)` follow the same option shape as `--cache`/`WithCache()`: a nullable caller override, resolved once per request. A supplied value `<= 0` is rejected the same way other invalid CLI/Testing inputs are (a thrown validation exception surfaced as a CLI runtime error / Testing API exception), never silently clamped. No upper bound is imposed on an explicit override beyond what `ParallelOptions.MaxDegreeOfParallelism` itself accepts, matching the issue's "positive caller overrides are bounded and validated" (bounded by hardware reality through `ParallelOptions`, not by an arbitrary ceiling this change invents).

### `Counters.Concurrency` reports `NotApplicable` when nothing actually ran in parallel
`Status` is set to `Active` only when at least one phase in the run actually took the parallel path (partition count `>= 2` and resolved max-parallelism `> 1`); an all-sequential run (tiny target set or `--max-parallelism 1`) reports `NotApplicable` with all-zero fields, consistent with the existing "reserved fields report not-applicable today" scenario now becoming "reports not-applicable when parallelism didn't actually apply."

## Risks / Trade-offs

- **[Risk]** A subtle bug in the merge step could silently reorder output only under real multi-core parallelism, which single-threaded CI could miss. → **Mitigation**: dedicated tests run the same fixture at `--max-parallelism 1` and `--max-parallelism 4` (or higher, machine-dependent) and assert byte-identical serialized output/ordering, plus a stress test with an artificially small threshold override (if needed, via internal test-only construction options already used by `ArchitectureSourceFileFactIndex.ConstructionOptions`) to force the parallel path on small fixtures.
- **[Risk]** `Parallel.For`'s own exception handling wraps thrown exceptions in `AggregateException`, which could change `OperationCanceledException` propagation shape expected by `cooperative-cancellation`. → **Mitigation**: the helper explicitly unwraps a single `OperationCanceledException` from any caught `AggregateException`/`OperationCanceledException` and rethrows it directly, verified by a cancellation-mid-scan test for both phases.
- **[Risk]** Reflection (`Assembly.GetTypes()`) calls across assemblies loaded into the same `AssemblyLoadContext` might not be fully thread-safe in every runtime edge case. → **Mitigation**: scope is limited to `Assembly.GetTypes()`/type enumeration, which is documented as safe for concurrent read access in .NET; no assembly loading happens inside the parallel partitions (assemblies are already fully resolved/loaded before `LoadAllTypes`/`BuildData` run).
- **[Trade-off]** Holding all partition results in memory before merging increases peak working set slightly versus a streaming sequential accumulator. → Accepted: partition result sizes are bounded by the same assembly/file set the sequential path already fully materializes.

## Migration Plan

Purely additive and opt-in-by-default-value: existing callers who never pass `--max-parallelism`/`WithMaxParallelism()` get the new default-parallel behavior automatically, but since output is guaranteed identical to the prior sequential behavior at any parallelism level, this is not a breaking change. No data migration. Rollback is a plain revert; no persisted format changes.

## Open Questions

None — scope, default, and exclusions are fully determined by the closed dependency issues (#365, #374, #375) and the issue's own explicit non-goals.
