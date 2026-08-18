## Why

`release-architecture-forensics` (#235) fixed the deterministic theory but ships no
executable surface. Issue #236 owns the first executable slice: a `history` command
family plus the canonical Git ingestion pipeline that turns an explicit authored
range into canonical commits, task-key provenance, rename lineage provenance, and
canonical file events.

Without a reviewed CLI/ingestion contract, the downstream scoring, graph, and report
tasks (#237–#244) have nothing stable to consume, and the fail-closed boundary
between a successful ingestion result and an error diagnostic stays undefined.

## What Changes

- Add an `arch-linter-net history` command family whose first subcommand runs
  canonical Git ingestion over an explicit `--from`/`--to` range.
- Implement the canonical ingestion pipeline inside `ArchLinterNet.Core`: repository
  object-format detection, deterministic authored-ref resolution, raw commit-object
  metadata parsing, exact author/committer grammar, arbitrary-precision committer
  epoch integers, `encoding ` header provenance, TaskKey extraction with mandatory
  provenance, reachability range, strict UTF-8 Git paths, baseline same-path
  identity, DAG-safe exact-rename lineage, and canonical file events with LCS line
  churn.
- Emit a deterministic minimal ingestion result (canonical JSON or a text summary)
  that exposes the evidence #235 declares mandatory, so downstream tasks consume a
  stable producer rather than re-deriving Git semantics.
- Emit a separate stable diagnostic surface for every fail-closed error so no partial
  ingestion result is ever produced.

## Capabilities

### New Capabilities

- `release-forensics-history-cli`: Defines the `history` command family, its authored
  operands, its deterministic minimal ingestion result, and its fail-closed
  diagnostic surface.

### Modified Capabilities

- None. `release-architecture-forensics` remains the semantic authority; this change
  implements its ingestion half without altering it.

## Impact

Adds an internal `ArchLinterNet.Core.History` ingestion pipeline consumed by the CLI
through the existing reviewed friend-assembly seam, so the reviewed public API
snapshots are unchanged. Adds one CLI command module. Introduces no new package
dependency and does not alter existing validation, policy, coverage, or badge
behavior.
