## Why

Baseline authoring is the one place where the tool writes a reviewed, human-owned file. Today `baseline generate`, `update`, and `prune` write that file immediately, unconditionally, and non-atomically: there is no way to see the proposed content first, an existing baseline is replaced without any statement of intent, a reviewed header comment is silently deleted, hand-recorded issue metadata is lost, and a failed write can leave the original truncated. `verify`/`diff` also report their categories under names that do not line up with what `update`/`prune` actually did, so a reviewer cannot follow one entry across the lifecycle.

## What Changes

- Add `--dry-run` and stdout preview to `generate`, `update`, and `prune` so every write can be reviewed before it happens; omitting `--output` writes the proposed document to stdout instead of a file.
- Add one shared baseline entry lifecycle model (`new`, `added`, `existing`, `kept`, `changed`, `resolved`, `stale`, `ambiguous`, `configuration`) used by human and JSON output of every baseline subcommand, so `update`/`prune` previews and `diff`/`verify` reports describe the same entry with the same word.
- Require explicit overwrite intent: `generate` refuses to replace an existing `--output` file without `--force`; in-place `update`/`prune` (`--output` equal to `--baseline`) remain the documented flow and stay allowed.
- Preserve the reviewed leading comment header and per-entry `issue` metadata across `update`/`prune`; when the existing file carries comments that cannot be safely round-tripped, refuse to write and report an actionable diagnostic naming the offending lines and the `--dry-run` path to a manual merge.
- Make every baseline write atomic (temp file plus rename), so a failed write never damages the original file.
- Add per-contract and per-family reason mapping (`--reason-for-contract`, `--reason-for-family`) applied to newly added entries only; entries carried through keep their `reason` and `issue` verbatim.
- Report lifecycle counts and the canonical structured identity of every entry in `diff --json` and `verify --json`, including an `ambiguous` classification for a baseline entry that correlates to more than one current candidate; `verify` fails closed on ambiguity.
- Document the distinct roles of `generate`, `migrate`, `update`, `prune`, and `verify`, and state that CI runs read-only baseline commands only.

## Capabilities

### Modified Capabilities

- `baseline-generation`: Baseline writes become previewable, explicitly intentional, comment- and metadata-preserving, atomic, and reported through one lifecycle vocabulary shared by human and JSON output.

## Impact

Affected areas are the baseline application service, comparer, generator, loader model, the six baseline CLI subcommands and their options and help texts, the baseline JSON schema (`issue` metadata), the migration-baselines and CI-integration guides, and NUnit tests in `ArchLinterNet.Core.Tests` and `ArchLinterNet.Cli.Tests`. Existing baseline files keep loading unchanged; every new option is additive, and the only behavior change to an existing invocation is that `generate` now refuses to silently replace an existing output file and that `verify` now fails on ambiguous entries.
