## Why

Read-only Health and topology diagnostics can fail on consumer dependency versions already loaded by the CLI, and one topology type capture can report success with an incomplete subject set. The existing receipt-backed preparation path already verifies selected outputs and loads them in an isolated scope, but standalone Health drops that selection and topology readers cannot request it.

## What Changes

- Preserve prepared-receipt selection through standalone Health analysis.
- Add `--use-prepared-receipts` to topology capture, diff, and verify so readers use the existing receipt verification and isolated loading path without building or restoring.
- Keep topology and Health behavior equivalent to their prepared reference runs; reject stale or unverifiable receipts and incomplete type observations.
- Document the producer/reader workflow and add packaged CLI regressions that verify independent processes, complete observations, and unchanged selected artifact bytes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: extend explicit prepared-receipt selection to Health and topology readers.
- `assembly-resolution`: isolate receipt-verified consumer assemblies from the CLI load context.
- `architecture-health-summary`: preserve receipt selection in standalone Health snapshots.
- `topology-review-workflow`: support no-build receipt-backed capture, diff, and verify.

## Impact

Affected Core snapshot and assembly-resolution paths, CLI Health and topology request mapping, topology command help and user guidance, and packaged CLI acceptance coverage. No public Core API or receipt format change is intended.
