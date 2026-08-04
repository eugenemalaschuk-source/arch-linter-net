## Why

Issue #408 (parent story #354) asks for bounded parallel assembly/fact scanning as a measured optimization once its foundations exist: #365 (persistent cache) and #375 (cooperative cancellation) are closed, and #374 shipped the `analysis-profile/v1` counters (including an explicitly reserved, all-zero `Counters.Concurrency` section) that this change is meant to populate. Today `ArchitectureTypeIndex.LoadAllTypes()` and `ArchitectureSourceFileFactIndex.BuildData()` scan every target assembly / source root strictly sequentially even though each assembly's/root's work is independent and read-only, so wall-clock scanning time on multi-assembly/multi-file repositories scales linearly with assembly/file count with no way to trade available cores for latency. Contract-family execution, by contrast, is not a safe parallelization target: `ArchitectureContractExecutor.Execute` assigns canonical finding identity from a shared, monotonically-growing `_findingIdentityCandidates` list using a cursor captured immediately before each contract runs, so parallelizing contract execution would corrupt identity attribution — this change deliberately excludes it.

## What Changes

- Add a new bounded-parallelism option (`--max-parallelism <n>` on the CLI `validate` command, `ArchitectureValidationBuilder.WithMaxParallelism(int?)` on the Testing API) that is validated (positive integers only), defaults to `max(1, min(Environment.ProcessorCount, 4))` when unset, and is a first-class supported sequential mode at `1`.
- Thread the resolved max-parallelism value into `ArchitectureAnalysisContext` so scanning phases can read it without a new cross-cutting parameter on every call.
- Parallelize `ArchitectureTypeIndex.LoadAllTypes()`: partition per target assembly, run `Assembly.GetTypes()`-based loading for each assembly concurrently bounded by the resolved degree, and flatten results back into the exact original assembly-iteration order — never assembly-completion order — so the produced `Type[]` is byte-for-byte identical to today's sequential output regardless of scheduling.
- Parallelize `ArchitectureSourceFileFactIndex.BuildData()`'s reflection pass (per pre-sorted assembly) and source-enumeration pass (per source root) the same way: partition work, compute each partition's facts independently and concurrently, then merge deterministically by the existing pre-established key order (already-sorted assembly order for reflection, source-root declaration order for source scan) before the existing final `SortFactsAndAmbiguities` sort runs — the final `allFacts`/`ambiguities` output is unchanged in content and order at any parallelism level.
- Skip the parallel code path entirely (run the existing sequential loop) whenever the work-item count for a given phase is below a small fixed threshold or `--max-parallelism 1` is requested, so small repositories and explicit sequential mode avoid `Parallel`/`Task` setup overhead.
- Populate `AnalysisProfileCounters.Concurrency` (`AnalysisProfileConcurrencyCounters`) with real data — configured/effective max parallelism, scheduled and completed work-item counts, observed maximum concurrent workers, and deterministic-merge counts — setting `Status` to `Active` whenever a parallel-eligible phase ran, and leaving it `NotApplicable`/all-zero when every phase in the run took the sequential path (e.g. trivially small target sets or `--max-parallelism 1`).
- Preserve every existing cancellation guarantee: bounded parallel loops observe the session's `CancellationToken` (via `ParallelOptions.CancellationToken`), and a cancellation observed before a phase's deterministic merge step completes discards that phase's partial parallel work instead of publishing it, consistent with the existing "cancellation observed before publication wins" contract.
- No change to contract-family execution, finding-identity assignment, cache lookup/population ordering, or any other phase outside `ArchitectureTypeIndex`/`ArchitectureSourceFileFactIndex` — this keeps the change scoped to the two phases proven safe to parallelize (see design.md for the full phase inventory and exclusions).

## Capabilities

### New Capabilities
- `bounded-parallel-scanning`: the `--max-parallelism`/`WithMaxParallelism()` option surface, its validated default/override resolution, and the deterministic bounded-parallel execution contract for type loading and source-file fact indexing.

### Modified Capabilities
- `analysis-profile`: `Counters.Concurrency` moves from an always-reserved, always-`NotApplicable` placeholder to a populated section reporting real scheduling/completion/concurrency/merge counts whenever a parallel-eligible phase executed.

## Impact

- Changed code in `src/ArchLinterNet.Core/Execution/ArchitectureTypeIndex.cs`, `src/ArchLinterNet.Core/Execution/ArchitectureSourceFileFactIndex.cs`, `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisContext.cs`, `src/ArchLinterNet.Core/Execution/ArchitectureRunnerSetupService.cs`.
- New code under `src/ArchLinterNet.Core/Execution/` for the bounded-parallel partition/merge helper(s) and max-parallelism resolution/validation.
- Changed `src/ArchLinterNet.Core/Profiling/AnalysisProfileConcurrencyCounters.cs` and `AnalysisProfileBuilder.cs` to populate real counters.
- New CLI option in `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandDefinition.cs` and its handler/options plumbing, mirroring the existing `--cache`/`--profile` pattern.
- New Testing API method in `src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs`.
- New tests proving concurrency-1/4 equivalence, byte-stable ordering, and cancellation-safety for both parallelized phases.
- No breaking changes: omitting `--max-parallelism`/`WithMaxParallelism()` preserves the existing default (a bounded worker count, not literal sequential execution before this change existed the phases were already single-threaded, so behavior/output is unchanged — only wall-clock scheduling changes).
