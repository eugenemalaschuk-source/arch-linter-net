# 0.5.1 Release Notes

0.5.1 is the single public adoption-stabilization release. Checkpoint A is
internal evidence only; the final packed-artifact Checkpoint B is the release
authorization gate.

## Adoption and compatibility

- Existing root policies and imported fragments retain supported 0.5.0 meaning
  unless an explicit 0.5.1 capability or documented correctness
  requalification applies.
- Baseline v1 remains legacy exact matching; baseline v2 uses reviewed,
  structured identity. Migration, update, prune, diff, and verify are explicit
  operations and never automatically approve debt.
- Public API snapshots are deterministic, reviewed artifacts: capture and
  update require explicit writing intent; diff is read-only.
- `finding/v1` supplies normalized typed details and canonical identity across
  human, JSON, SARIF, Testing, and baseline workflows.

## Execution and output

- `policy check` validates static policy/configuration without requiring target
  assemblies; `--ensure-built` and `--no-restore` make clean-checkout and
  restricted-environment preparation explicit.
- Repeatable `--report <format>=<destination>` routes one validation result to
  human, JSON, and SARIF sinks. Command-owned `--output` artifacts retain their
  own reviewable semantics.
- `analysis-cache/v1` is opt-in and verified; `analysis-profile/v1` is
  opt-in measurement; `--max-parallelism 1` remains a supported sequential
  mode. These controls do not change canonical findings.
- Cancellation before publication returns typed `cancelled` completion and exit
  `2`; output failures report `output-failed` or `partial-output` without
  claiming a cross-file transaction.

## Offline contract

Installed CLI and applicable packages contain the immutable
`adoption-stabilization/v1` registry and release-qualified 0.5.1 schemas for
policy root/fragment, baseline, API snapshot, normalized finding, build state,
cache, and profile formats. Use `schema list` and `schema print` from the
installed tool as the offline compatibility authority.

## Where to start

- [Adopt or upgrade to 0.5.1](../guides/migration-to-0-5-1.md)
- [0.5.1 reference entrypoints](../guides/reference-entrypoints.md)
- [CLI usage](../cli/index.md)
- [YAML schema reference](yaml-schema.md)
- [Release process](release-process.md)
