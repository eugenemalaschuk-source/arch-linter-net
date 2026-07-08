## 1. Core: shared comparison primitive

- [x] 1.1 Add `ArchitectureBaselineComparer` (or equivalent) in `src/ArchLinterNet.Core/Contracts/` that classifies each baseline entry as frozen/resolved/configuration-error and each candidate with no matching entry as new, keyed by `(contract id, source_type, forbidden_reference)`, reusing the existing dedup/matching approach from `ArchitectureBaselineLoadingService`'s `ContractGroupMerger`.
- [x] 1.2 Add unit tests for the comparer covering: frozen match, resolved (no match, known id), configuration error (unknown id), new candidate (no baseline entry).

## 2. Core: update

- [x] 2.1 Add `BaselineUpdateRequest`/`BaselineUpdateOutcome` records in `src/ArchLinterNet.Core/Validation/`.
- [x] 2.2 Add `Update(BaselineUpdateRequest)` to `IArchitectureBaselineApplicationService` and implement it: load existing baseline, run validation/candidate-collection, keep frozen entries' `reason` verbatim, keep resolved/configuration-error entries untouched, append new entries via existing generator logic, serialize to `--output`.
- [x] 2.3 Add `UpdateBaseline(request)` on `ArchitectureEngine`.
- [x] 2.4 Core.Tests: reason preserved on frozen entries; new entries added deterministically; stale entries not removed; `--condition-set` and `--contract` scoping honored.

## 3. Core: prune

- [x] 3.1 Add `BaselinePruneRequest`/`BaselinePruneOutcome` (outcome includes removed-entry list with reason tag) records.
- [x] 3.2 Add `Prune(BaselinePruneRequest)` to the application service: load baseline, classify via comparer, drop resolved + configuration-error entries, serialize survivors to `--output`, return removed entries with reasons.
- [x] 3.3 Add `PruneBaseline(request)` on `ArchitectureEngine`.
- [x] 3.4 Core.Tests: resolved entries removed and reported; configuration-error entries removed and reported; frozen-only baseline is a no-op; new violations are not added.

## 4. Core: diff and verify

- [x] 4.1 Add `BaselineDiffRequest`/`BaselineDiffOutcome` (all four categories) and `BaselineVerifyRequest`/`BaselineVerifyOutcome` (pass/fail + same category detail) records.
- [x] 4.2 Add `Diff(BaselineDiffRequest)` and `Verify(BaselineVerifyRequest)` to the application service, both read-only, both built on the shared comparer.
- [x] 4.3 Add `DiffBaseline(request)` and `VerifyBaseline(request)` on `ArchitectureEngine`.
- [x] 4.4 Core.Tests: diff reports all four categories correctly; diff exits-equivalent success regardless of counts; verify fails on resolved debt; verify fails on configuration error; verify passes with only new debt present; verify passes when fully in sync.

## 5. CLI: baseline subcommand dispatch

- [x] 5.1 In `src/ArchLinterNet.Cli/Program.cs`, change `RunBaselineCommand` to explicitly dispatch on `generate|update|prune|diff|verify` (replacing the current silent-fallthrough-to-generate behavior).
- [x] 5.2 Implement `RunBaselineUpdateCommand`, `RunBaselinePruneCommand`, `RunBaselineDiffCommand`, `RunBaselineVerifyCommand`, each parsing `--policy`/`--config`, `--baseline`, `--output` (update/prune only), `--mode`, `--condition-set`, `--contract` (repeatable), `--reason` (update only), `--json`, `-h`/`--help`, following the existing flag-parsing pattern used by `RunBaselineCommand`/`RunValidateCommand`.
- [x] 5.3 Add `--contract` support to the existing `generate` sub-verb parsing.
- [x] 5.4 Add `PrintBaselineUpdateHelp`, `PrintBaselinePruneHelp`, `PrintBaselineDiffHelp`, `PrintBaselineVerifyHelp`, and update `PrintBaselineHelp`/root `PrintHelp` usage text to list all five subcommands.
- [x] 5.5 Wire human-readable and `--json` output formatting for update/prune (files written + summary) and diff/verify (categorized report), matching existing formatter conventions in `Program.cs`.
- [x] 5.6 Confirm exit codes: 0 success (update/prune/diff always on successful run; verify only when in sync), 1 for verify-out-of-sync, 2 for argument/runtime errors — consistent with existing commands' 0/1/2 convention.

## 6. CLI integration tests

- [x] 6.1 Add `CliIntegrationTests.BaselineUpdate.cs`, `BaselinePrune.cs`, `BaselineDiff.cs`, `BaselineVerify.cs` under `tests/ArchLinterNet.Cli.Tests`, mirroring the existing `CliIntegrationTests.BaselineGenerate.cs` structure.
- [x] 6.2 Cover: `--condition-set` scoping, `--contract` (selected-contract) scoping, stale-ignore detection (resolved entries surfaced correctly), `--json` output shape, and help text for each new subcommand.
- [x] 6.3 Add a CLI test for `--contract` on `baseline generate`.

## 7. Docs and spec sync

- [x] 7.1 Update `docs/guides/migration-baselines.md` to document `update`/`prune`/`diff`/`verify` as first-class lifecycle commands (replacing the manual regenerate/hand-edit workflow description where applicable).
- [x] 7.2 Update `docs/cli/index.md` with the new subcommands and flags.
- [x] 7.3 Reconcile `schema/baseline.schema.json` if any gaps are found during implementation (note: schema is currently missing `strict_project_metadata`/`audit_project_metadata`/`strict_coverage`/`audit_coverage` groups — out of scope for this change unless it blocks implementation, otherwise flag separately).
- [x] 7.4 Run `openspec archive add-baseline-lifecycle-commands` after implementation, tests, and docs are complete; then run `openspec validate --all`.

## 8. Validation

- [x] 8.1 Run `make fmt`.
- [x] 8.2 Run `make acceptance` and fix any failures.
