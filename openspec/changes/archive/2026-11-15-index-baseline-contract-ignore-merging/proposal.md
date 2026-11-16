# Proposal

## Why

Baseline merge work currently grows with a contract-group scan for each baseline entry and a growing ignore-list scan for each unique ignore. Large policies and baselines therefore make loading unnecessarily quadratic, despite the merge needing only exact contract and identity membership checks.

## What Changes

- Index contracts within each group while retaining case-insensitive ID matching and first-match behavior.
- Index each touched contract's existing and newly accepted ignores using the exact legacy or structured merge key for the baseline version.
- Preserve ordered ignore lists, inline-before-loaded precedence, first reason/provenance, and all existing filtering and diagnostics.
- Add deterministic scale evidence and regression coverage for independently varying contracts, baseline entries, and ignores.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `baseline-generation`: Specify linear expected merge work in the number of contracts, baseline entries, and ignores while retaining existing baseline semantics.

## Impact

The change is limited to Core baseline loading/merge implementation and focused Core tests. It adds no public API, baseline format, package dependency, or release-scope change. It is post-release 0.10 performance stabilization and does not authorize publication.