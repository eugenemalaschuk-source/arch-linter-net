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

### One lifecycle vocabulary, taken from the authoritative capability

The vocabulary is not ours to invent. `adoption-stabilization-compatibility` already fixes it for the
whole tool, and every baseline subcommand must classify into exactly these seven values:

| Value | Meaning |
| --- | --- |
| `new` | a current finding has no exact baseline entry |
| `matched` | an entry and a current finding have equal canonical identity |
| `resolved` | a valid, evaluable entry has no current finding — the debt was fixed |
| `stale` | the entry references a contract, family, source, schema, or identity form no longer valid or evaluable |
| `changed` | a predecessor/successor relationship is derivable but canonical identity differs, so the entry does not suppress |
| `ambiguous` | more than one candidate could correspond to the entry |
| `configuration-error` | malformed, unsupported, or inconsistent input prevents safe classification |

`BaselineEntryLifecycle` in `Core.Model` is exactly these seven, and `BaselineEntryLifecycleNames.All`
is asserted against the literal list in a test, so a future command cannot quietly add an eighth.

Two consequences fall out of the definitions and are worth stating, because the obvious
implementation gets both wrong:

- **Disposition is a separate axis.** `update` retains a fixed-debt entry and `prune` removes it, but
  both are looking at the same `resolved` classification. Encoding that difference in the status would
  fork the vocabulary — which is precisely what a consumer branching on `status` cannot absorb. So
  entries carry a `disposition` of `reported`/`added`/`retained`/`removed` alongside `status`, and
  `--json` exposes both plus a boolean `suppresses`.
- **Display text is not identity, so refreshing it is `matched`, not `changed`.** `changed` is defined
  as canonical identity *differing*, and carries the requirement that such an entry must not suppress
  until reviewed. An entry whose identity still matches while its `forbidden_reference` string
  re-renders is fully matched; calling it `changed` would both misreport it and imply it stops
  suppressing. `changed` is therefore reserved for a genuine predecessor/successor relation, which this
  slice does not compute and consequently never emits — the vocabulary is shared in full, but no value
  is fabricated for a relation the tool cannot demonstrate.

An entry naming a contract id the policy no longer has is `stale`, not `configuration-error`: it
"references a contract that is no longer valid or evaluable", which is the definition. This reconciles
the transitional wording the consistency audit flagged — the shipped baseline-generation spec called
that case a configuration error and used `stale` for resolved debt, both of which this slice migrates.

`configuration-error` covers input that cannot be classified at all (a malformed reason mapping, an
unsupported `version`). Those surface as command-level errors rather than per-entry statuses today.

### Ambiguity is a comparison result, not a migrate-only concept

`ArchitectureBaselineComparer` counts matching candidates instead of short-circuiting on the first. Zero matches keeps its current meaning; exactly one is `Existing`; more than one is `Ambiguous`. A version-2 entry's structured identity normally determines at most one candidate, so in practice ambiguity surfaces for version-1 documents, whose legacy pair is precisely the under-specified identity the exact-identity work introduced structured fields to replace.

`verify` fails on `Ambiguous` alongside `Resolved` and `Stale`. An entry that suppresses more than one distinct violation is broadening the ratchet, which is the same failure `migrate` already refuses to write through; a gate that reported it and exited zero would let it persist indefinitely. `diff` stays a report and still exits zero.

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
