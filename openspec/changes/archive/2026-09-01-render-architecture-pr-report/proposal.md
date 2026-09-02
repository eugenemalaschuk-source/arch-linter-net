## Why

Architecture Health already determines whether a pull request is architecturally acceptable, but its canonical evidence is not yet available as one reviewer-oriented report. Reviewers need a deterministic local Markdown projection of the existing governance authorities without GitHub workflow code recreating health, waiver, policy, topology, or external-evidence semantics.

## What Changes

- Add a Core-owned, versioned architecture PR-report input and projection over canonical Architecture Health receipts and a canonical architecture-change report.
- Extend the machine-readable Health artifact with the canonical report evidence required for downstream projection; it remains an additive representation of the existing Health result, not another health algorithm.
- Add the `report pr` CLI command to validate these artifacts and render deterministic, bounded architecture-only Markdown to stdout or an explicit output file.
- Cover clean, debt, blocking, waiver-lifecycle, incomplete applicability/topology, wrong-context external-evidence, and unavailable-evidence shapes with deterministic fixtures, and document the local CLI workflow.

## Capabilities

### New Capabilities

- `architecture-pr-reporting`: a deterministic Core projection and CLI Markdown report that helps PR reviewers inspect canonical architecture governance evidence.

### Modified Capabilities

- `architecture-health-summary`: Health JSON exposes canonical, non-recomputed reporting evidence so a downstream PR projection does not manufacture counts, lifecycle detail, or completeness from summary reasons.
- `architecture-change-report`: the canonical change artifact retains resolved findings so downstream reporting can distinguish resolved from newly added and continuing findings without comparing snapshots again.

## Impact

- `ArchLinterNet.Core` health serialization, report input validation/projection, and tests.
- `ArchLinterNet.Cli` command-module surface, report command/renderer, integration tests, and packaged-consumer acceptance.
- CLI and output-format documentation plus the reviewed Core public-API snapshot if the public Core composition surface grows.
