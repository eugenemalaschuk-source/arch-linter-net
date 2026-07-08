## Context

`baseline generate` (see `openspec/specs/baseline-generation/spec.md`) writes a fresh baseline from current violations every time. It has no notion of an existing baseline file, so any change to the codebase requires regenerating the whole file, which silently discards any hand-edited `reason` text on entries that are still valid debt. There is also no way to inspect drift (has debt been resolved? is anything stale?) without diffing YAML by hand, and no CI-friendly way to assert "the baseline is still in sync."

The CLI (`src/ArchLinterNet.Cli/Program.cs`) is a single `Program` class with hand-rolled arg parsing per verb; `RunBaselineCommand` currently special-cases only `generate`. The engine layer (`ArchitectureEngine.GenerateBaseline` → `IArchitectureBaselineApplicationService.Generate` → `ArchitectureBaselineGenerator`) already knows how to run validation and collect `ArchitectureBaselineCandidate`s (contract group, contract id, source_type, forbidden_reference) deterministically; the new commands reuse this candidate collection rather than re-implementing it.

## Goals / Non-Goals

**Goals:**
- Add `baseline update`, `baseline prune`, `baseline diff`, `baseline verify` as CLI subcommands, following the existing `Program.cs` command pattern (parse flags → build request record → call engine → format output → exit code).
- Preserve `reason` text on baseline entries that are still valid across `update`.
- Give `prune` and `diff` a shared way of classifying baseline entries against current candidates: **frozen** (still matches a candidate), **resolved/stale** (no matching candidate, id is known), **configuration error** (contract id not in the current policy), and, for diff only, **new** (candidate with no matching baseline entry).
- Make `verify` a pure CI gate: exit 0 if no resolved/stale/configuration-error entries exist, non-zero otherwise. Never writes a file.
- Extend `--contract` (repeatable selected-contract filter) to `generate` and all four new subcommands for consistency with `validate`/`graph`/`explain`.
- Keep the baseline file format, JSON schema, and `validate --baseline` merge/consumption path completely unchanged.

**Non-Goals:**
- No interactive/dry-run confirmation gate before writing — as today with `generate`, `update`/`prune` write directly to `--output`; reviewability comes from the PR diff of the YAML file (explicit non-goal in the issue).
- No wildcard/glob baseline entries — matching stays exact `(source_type, forbidden_reference)` pairs.
- No change to how `validate --baseline` merges or reports unmatched ignores.

## Decisions

### 1. Shared comparison primitive: `BaselineComparisonService` (or method on the existing application service)
`update`, `prune`, `diff`, and `verify` all need the same underlying computation: given a loaded `ArchitectureBaselineDocument` and the current run's `ArchitectureBaselineCandidate`s, produce, per contract group/id:
- entries with a matching candidate → **frozen**
- entries with no matching candidate but a known contract id → **resolved**
- entries whose contract id doesn't exist in the current policy → **configuration error**
- candidates with no matching entry → **new**

This is implemented once (e.g. `ArchitectureBaselineComparer.Compare(document, candidates, policyContractIds)` in `src/ArchLinterNet.Core/Contracts/`) and reused by all four verbs, instead of four separate ad-hoc diff implementations. This avoids duplicating the matching/dedup key logic that already lives in `ArchitectureBaselineLoadingService`'s `ContractGroupMerger`.

**Alternative considered**: implement diff/verify logic as a thin wrapper solely inside `diff`, and have `update`/`prune` do their own pass. Rejected — it would duplicate the classification logic four times and risk them drifting out of sync (e.g. "stale" meaning something subtly different in `prune` vs `verify`).

### 2. `update` = preserve frozen entries' reason + append new entries
`update` runs the comparison, keeps all **frozen** entries with their original `reason` verbatim, keeps **resolved** and **configuration-error** entries as-is (untouched — removal is `prune`'s job, not `update`'s), and appends new entries for **new** candidates using the default/`--reason` text — the same generation logic `ArchitectureBaselineGenerator` already uses. Output is written to `--output` (same flag as `generate`).

### 3. `prune` = drop resolved + configuration-error entries, report what was removed
`prune` runs the same comparison, keeps only **frozen** entries, drops **resolved** and **configuration-error** entries, writes the result to `--output`, and prints (human text and `--json`) the list of removed entries tagged with why (`resolved` vs `configuration-error`). `new` candidates are *not* added by `prune` (that's `update`'s or `generate`'s job) — this keeps `prune` a single-purpose "remove stale" operation.

### 4. `diff` = read-only report of all four categories, `verify` = read-only pass/fail
`diff` runs the comparison and reports all four categories (new / frozen / resolved / configuration-error) without writing a file; exit code is always 0 (it's a report, not a gate) unless there's a hard error (bad policy, IO failure) which uses the existing exit-code-2 convention.
`verify` runs the same comparison and exits 1 if `resolved` or `configuration-error` counts are non-zero (baseline out of sync), 0 otherwise. It reports counts/details the same way `diff` does, so users can run `verify` in CI and `diff`/`prune` locally to fix it.

### 5. Request/outcome shape mirrors existing `BaselineGenerationRequest`/`Outcome`
New records: `BaselineUpdateRequest`, `BaselinePruneRequest`, `BaselineDiffRequest`, `BaselineVerifyRequest` (each: `PolicyPath`, `BaselinePath`, `Mode`, `ConditionSetName`, `ContractIds`, plus `OutputPath`/`Reason` where applicable) and matching `*Outcome` records, following the same field naming as `BaselineGenerationRequest`/`BaselineGenerationOutcome`. New methods added to `IArchitectureBaselineApplicationService` (or the interface is split if it grows unwieldy — TBD during implementation, default to extending the existing interface since it already owns baseline generation) and forwarded through `ArchitectureEngine`.

### 6. CLI dispatch: extend `RunBaselineCommand` sub-verb switch
`RunBaselineCommand(args)` currently only recognizes `generate` as an explicit sub-verb (and silently falls through to generate behavior otherwise). This changes to an explicit switch on `args[0]` (`generate|update|prune|diff|verify`), each delegating to its own `RunBaseline<Verb>` method with its own flag parsing and `PrintBaseline<Verb>Help`, matching the existing per-command help pattern (`PrintGraphHelp`, `PrintExplainHelp`).

## Risks / Trade-offs

- **Risk**: Growing `IArchitectureBaselineApplicationService` to five methods makes it a wide interface. → Mitigation: keep it as one interface for now (it's already the sole owner of baseline lifecycle concerns); revisit only if a future contract adds unrelated concerns.
- **Risk**: Reusing `reason` verbatim on `update` could preserve a stale/misleading reason if the same `(source_type, forbidden_reference)` reappears for an unrelated cause after being removed and re-added. → Mitigation: matching is keyed by `(contract id, source_type, forbidden_reference)`, same exactness as existing merge logic; this is an accepted edge case already implicit in how baselines work today (identical pairs are indistinguishable).
- **Risk**: `--contract` added to `generate` changes existing CLI surface. → Mitigation: purely additive/optional flag, default behavior (no `--contract`) unchanged — backward compatible.
- **Trade-off**: No dry-run flag for `update`/`prune` means a mistaken run requires `git checkout` to undo. → Accepted per the issue's non-goal ("not replacing source-control review") — the file is expected to be committed and reviewed via a PR diff, same as `generate` today.

## Open Questions

- Exact flag name for CI-friendliness on `verify` (e.g. should it also support `--json` for machine-readable CI output)? Default to yes, consistent with other commands, decided during implementation unless it proves awkward.
