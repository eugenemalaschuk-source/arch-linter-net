## Why

Policy weakening currently captures approval evidence from the default ordinary build state. That can read stale assemblies and reject an otherwise exact public API approval, while claiming live CLR evidence without verifying that it represents the current source. Approval evidence must come from an explicitly prepared and verified build.

## What Changes

- Add `--condition-set`, `--ensure-built`, and `--no-restore` to `policy weakening` and pass them to public API live capture.
- Require `--ensure-built` when a public API approval is supplied; keep artifact-only comparisons unchanged when there is no approval.
- Add regression coverage for stale ordinary evidence, exact verified additions, and fail-closed approval cases; document the command options.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-weakening-guardrails`: approved API additions require live CLR evidence captured from a fresh verified build state.

## Impact

Changes the ArchLinterNet CLI policy-weakening command and its options, tests, and CLI reference. It does not change the approval schema, comparator, snapshot grammar, or public API capture implementation.
