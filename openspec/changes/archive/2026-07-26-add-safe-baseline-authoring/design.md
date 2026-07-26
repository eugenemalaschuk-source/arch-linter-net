# Design — Safe and reviewable baseline authoring

## Context

`baseline generate|update|prune|diff|verify|migrate` already share candidate collection (`ArchitectureBaselineApplicationService.CollectCandidates`) and a single comparer (`ArchitectureBaselineComparer.Compare`), which classifies baseline entries into `New`, `Frozen`, `Resolved`, `ConfigurationErrors`, and `OutOfScope`. The write commands consume that classification and immediately call `IFileSystem.WriteAllText`.

Three problems follow from that shape:

1. The classification names are internal to comparison. `update` reports "preserved N, added M"; `prune` reports "removed N"; `diff`/`verify` report "new/matched/stale/configuration_error". The same entry has three different names depending on which command a reviewer ran.
2. Nothing sits between classification and the write. There is no preview, no overwrite gate, no atomicity, and the YAML is rebuilt from the classified entries only — anything in the original file that is not a classified entry (comments, unmodelled per-entry metadata) is dropped.
3. Comparison answers "does at least one candidate match?" (`HasMatchingCandidate*` returns `bool`). It cannot distinguish "exactly one" from "more than one", which is the ambiguity `migrate` already treats as fail-closed but `verify` cannot see.

## Goals / Non-Goals

Goals:

- One lifecycle vocabulary across every baseline subcommand, in human and JSON output.
- Every write previewable, explicitly intended, atomic, and non-destructive of reviewed content.
- Reason assignment expressive enough for per-contract and per-family adoption without touching carried-through entries.

Non-Goals:

- A comment-preserving YAML round-trip engine. YamlDotNet's serializer does not emit comments; building a full round-trip layer is out of proportion to the requirement, which explicitly allows reporting that a safe update is unavailable.
- Automatic approval of new debt, reformatting unrelated YAML, or any change to `--baseline` matching semantics at `validate` time.
- Re-doing the exact-identity work from the exact-baseline-identity change.

## Decisions

### One lifecycle enum, distinct write and report vocabularies

`BaselineEntryLifecycle` lives in `ArchLinterNet.Core.Model` with one canonical lowercase wire name per value:

| Value | Wire name | Meaning |
| --- | --- | --- |
| `New` | `new` | current violation with no baseline entry (report vocabulary) |
| `Added` | `added` | that violation materialized as a new entry by this write |
| `Existing` | `existing` | baseline entry that still matches exactly one current candidate (report vocabulary) |
| `Kept` | `kept` | that entry carried into the output byte-for-byte |
| `Changed` | `changed` | that entry carried into the output with a non-identity field regenerated |
| `Stale` | `stale` | entry matching no current candidate that this operation did not remove |
| `Resolved` | `resolved` | entry matching no current candidate that this operation removed |
| `Ambiguous` | `ambiguous` | entry correlating to more than one current candidate |
| `Configuration` | `configuration` | entry whose contract id does not exist in the policy |

The split between report and write names is deliberate: `new`/`existing` describe what comparison found, `added`/`kept`/`changed` describe what a write did with it, and `resolved`/`stale` distinguish removal from retention of the same underlying condition. That is why `update` reports the same disappeared entry as `stale` (it retains it) while `prune` reports it as `resolved` (it removes it) — one model, two truthful outcomes, no renaming of categories per command. `stale` keeps the wire value today's `diff`/`verify` JSON already emits for a no-longer-matching entry, so no existing consumer breaks; `resolved` is the value only a removing write can produce.

The `counts` object always carries all nine keys so a consumer can read one shape from every subcommand, with `0` where the operation cannot produce that value.

`Changed` covers the case where structured identity still matches but the display text regenerated from the live candidate differs from what the file recorded. `reason` and `issue` are never a source of `Changed`, because they are preserved verbatim for any carried-through entry.

Rejected alternative: a single flat set of five statuses. It cannot express "the entry disappeared and I kept it" versus "the entry disappeared and I deleted it" without the consumer knowing which command ran, which is exactly the coupling the shared model exists to remove.

### Ambiguity is a comparison result, not a migrate-only concept

`ArchitectureBaselineComparer` counts matching candidates instead of short-circuiting on the first. Zero matches keeps its current meaning; exactly one is `Existing`; more than one is `Ambiguous`. A version-2 entry's structured identity normally determines at most one candidate, so in practice ambiguity surfaces for version-1 documents, whose legacy pair is precisely the under-specified identity the exact-identity work introduced structured fields to replace.

`verify` fails on `Ambiguous` alongside `Stale` and `Configuration`. An entry that suppresses more than one distinct violation is broadening the ratchet, which is the same failure `migrate` already refuses to write through; a gate that reported it and exited zero would let it persist indefinitely. `diff` stays a report and still exits zero.

`update` and `prune` carry ambiguous entries through untouched rather than rewriting or removing them. Rewriting would have to pick one of several identities (silent broadening or silent narrowing); removing would delete accepted debt. Carrying through plus reporting leaves the decision with the reviewer, consistent with `migrate`'s fail-closed stance.

### Preview and write gating live in the CLI, classification stays in Core

Core gains the lifecycle-classified entry list and the proposed YAML on each outcome; it does not decide whether to write. The CLI owns `--dry-run`, stdout-versus-file destination, the overwrite gate, and the atomic write, matching how `public-api capture/update` already split those concerns.

`--output` omitted means "print the proposed document to stdout" for `generate`, `update`, and `prune`. That makes preview reachable without inventing a sentinel path, and it composes with shell redirection. `--dry-run` additionally suppresses the write when `--output` *is* given, and prints the lifecycle report plus the proposed document — the same shape `public-api update --dry-run` already produces.

### Overwrite intent

`generate --output <existing file>` refuses to run without `--force`, because generation discards whatever reviewed content that file held. `update`/`prune` with `--output` equal to the resolved `--baseline` path is the documented in-place lifecycle step and needs no flag: naming the same file twice *is* the statement of intent, and the content is derived from that file rather than replacing it wholesale. `update`/`prune` writing over some *other* existing file still requires `--force`.

### Comment preservation is header-only, with a fail-closed diagnostic

The leading run of comment and blank lines before the first non-comment line is the reviewed header (`# Baseline for …`, ownership, ticket links). `update`/`prune` capture it from the input file and re-emit it verbatim above the regenerated document.

Any comment line at or after the first non-comment line cannot be re-anchored: the serializer rebuilds the mapping from the model, and there is no stable relationship between an input line and an output line once entries are added, removed, or reordered. Guessing an anchor would silently move a reviewer's note onto the wrong entry. So `update`/`prune` refuse to write, report the 1-based line numbers of the unanchorable comments, and point at `--dry-run` to obtain the proposed document for a manual merge. `--dry-run` itself still works on such a file, which is what makes the refusal actionable rather than a dead end.

### Per-entry `issue` metadata

`ignored_violations` entries gain an optional `issue` string, added to both schema versions and never part of matching or deduplication. It is the modelled home for the tracking reference that adopters currently smuggle into `reason` prose, and it is what "retains reasons and issue metadata" requires be carried through untouched. Being modelled is what makes it survivable: an unmodelled key is dropped by the round-trip whatever the comment handling does.

### Reason mapping

`--reason-for-contract <id>=<text>` and `--reason-for-family <family>=<text>` are repeatable. Resolution order for a newly added entry is contract id, then contract family (the group name with its `strict_`/`audit_` prefix stripped, as `ArchitectureViolationIdentity.ResolveContractFamily` already computes), then `--reason`, then the built-in default. Mapping applies only to entries the operation adds; carried-through entries keep their recorded `reason`. A malformed pair (missing `=`, empty key, empty value) or a duplicate key is rejected up front rather than silently ignored.

### Atomic writes

Every baseline write goes through `IFileSystem.WriteAllTextToTemp` followed by `RenameTempToTarget`, the primitives `public-api` and the report sinks already use. A failure while producing content leaves the original file untouched because nothing has been renamed over it yet.

## Risks / Trade-offs

- **Behavior change for existing invocations.** `generate` over an existing file now needs `--force`, and `verify` now fails on ambiguity. Both are the point of the change (fail closed on unreviewed replacement and on broadening matches); both are documented, and `--force` is a one-token migration for scripts that intended replacement.
- **Header-only comment preservation is partial.** A file with per-entry comments cannot be updated in place. Mitigated by the diagnostic naming exact lines plus a working `--dry-run` that yields the content to merge; the alternative (silent loss) is what the requirement forbids.
- **Ambiguity surfaces mostly on version-1 files.** That is intended pressure toward `migrate`, and `update`/`prune` still function on those files without broadening anything.
