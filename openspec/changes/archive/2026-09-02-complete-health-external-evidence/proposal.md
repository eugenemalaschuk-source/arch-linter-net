## Why

The Health command currently persists the reporting shape for external evidence
without using the existing canonical binder to produce its trust receipts. As a
result, a policy that requires external evidence cannot produce a complete
pull-request report through the documented CLI workflow, even when a valid
zero-result SARIF artifact is supplied.

The same change must restore the checked-in detailed public API approval and
ensure that PR-report text cannot create GitHub links or mentions from
artifact-controlled values. Documentation must describe the supported legacy
Health behavior consistently.

## What Changes

- Extend `health` orchestration with the existing external-evidence bindings
  and producer context so its canonical outcome contains the trust receipts
  already required by the report-evidence contract.
- Exercise the full `health` to `report pr` CLI producer path for current
  zero-result evidence and wrong-revision evidence.
- Synchronize the detailed Core public-API approval with the additive report
  evidence API.
- Neutralize GitHub autolinks and mentions in artifact-controlled Markdown
  text, and document the legacy-Health unavailable-report behavior
  consistently.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-summary`: Health JSON report evidence must retain the
  canonical external-evidence trust receipts produced through the supported
  Health CLI workflow.
- `architecture-pr-reporting`: PR Markdown must keep artifact-controlled
  plain text inert with respect to GitHub autolinks and mentions.

## Impact

Affected areas are the Health CLI command/options and orchestration,
external-evidence binding reuse, Core report-evidence serialization, CLI
report rendering, Core API approval fixtures, CLI and Core tests, and user
documentation. No policy re-evaluation, SARIF parsing in the renderer, GitHub
API access, or publication behavior is introduced.
