## Why

Baseline authoring is the one place where the tool writes a reviewed, human-owned file. Today `baseline generate`, `update`, and `prune` write that file immediately, unconditionally, and non-atomically: there is no way to see the proposed content first, an existing baseline is replaced without any statement of intent, a reviewed header comment is silently deleted, hand-recorded issue metadata is lost, and a failed write can leave the original truncated. `verify`/`diff` also report their categories under names that do not line up with what `update`/`prune` actually did, so a reviewer cannot follow one entry across the lifecycle.

## What Changes

- Add `--dry-run` and stdout preview to `generate`, `update`, and `prune` so every write can be reviewed before it happens; omitting `--output` writes the proposed document to stdout instead of a file.
- Adopt the authoritative lifecycle vocabulary from `adoption-stabilization-compatibility` (`new`, `matched`, `resolved`, `stale`, `changed`, `ambiguous`, `configuration-error`) in the human and JSON output of every baseline subcommand, with a separate `disposition` axis (`reported`/`added`/`retained`/`removed`) so `update` and `prune` can act differently on one classification without renaming it, and a `suppresses` flag so only `matched` ever reads as suppressing a finding.
- Require explicit overwrite intent through one shared write gate used by `generate`, `update`, `prune`, and `migrate`: replacing an existing `--output` file needs `--force`, while in-place `update`/`prune` (`--output` equal to `--baseline`, compared case-sensitively) remains the documented flow.
- Preserve the reviewed leading comment header and per-entry `issue` metadata across `update`/`prune`; when the existing file carries comments that cannot be safely round-tripped, refuse to write and report an actionable diagnostic naming the offending lines and the `--dry-run` path to a manual merge.
- Make every baseline write atomic (temp file plus rename), so a failed write never damages the original file, and make a no-op `prune` reproduce its input byte-for-byte instead of reserializing it.
- Add per-contract and per-family reason mapping (`--reason-for-contract`, `--reason-for-family`) applied to newly added entries only; entries carried through keep their `reason` and `issue` verbatim.
- Report lifecycle counts and the canonical structured identity of every entry in `diff --json` and `verify --json`, including an `ambiguous` classification for a baseline entry that correlates to more than one current candidate; `verify` fails closed on `resolved`, `stale`, and `ambiguous`.
- Document the distinct roles of `generate`, `migrate`, `update`, `prune`, and `verify`, and state that CI runs read-only baseline commands only.

## Capabilities

### Modified Capabilities

- `baseline-generation`: Baseline writes become previewable, explicitly intentional, comment- and metadata-preserving, atomic, and reported through one lifecycle vocabulary shared by human and JSON output.

## Impact

Affected areas are the baseline application service, comparer, generator, loader model, the six baseline CLI subcommands and their options and help texts, the baseline JSON schema (`issue` metadata), the migration-baselines and CI-integration guides, and NUnit tests in `ArchLinterNet.Core.Tests` and `ArchLinterNet.Cli.Tests`. Existing baseline files keep loading unchanged; every new option is additive, and the only behavior change to an existing invocation is that `generate` now refuses to silently replace an existing output file and that `verify` now fails on ambiguous entries.
