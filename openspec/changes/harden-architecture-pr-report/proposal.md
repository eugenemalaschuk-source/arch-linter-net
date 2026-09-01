## Why

The architecture PR report can currently combine unrelated Health and change
artifacts, represent missing authority payload as complete, and render
untrusted repository-controlled text as active Markdown.  Those failures can
produce a convincing but semantically incorrect review result, so the report
must reject incompatible evidence and keep its presentation boundary safe.

## What Changes

- Persist a shared execution context in Health reporting evidence and
  architecture-change reports, and fail closed when `report pr` receives
  artifacts from different modes, condition sets, or executions.
- Validate report-evidence availability as a closed, bidirectional contract:
  known keys and tokens only, and each available authority payload must be
  present while each absent payload is explicitly unavailable or not
  configured.
- Project canonical blocking receipts independently of unrelated audit,
  external, configuration, or preflight findings; retain those as
  non-blocking evidence unless their owner marks them blocking.
- Escape untrusted report values by Markdown context, and omit empty blocker
  and debt drill-downs from a clean complete report.
- Move PR-report composition to `Core.Reporting`, use a report-owned change
  projection, and replace production partial types with focused collaborators
  without weakening architecture policy.
- Make resolved-finding comparison linear in the number of baseline and
  current findings.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-pr-reporting`: Require compatible execution contexts,
  fail-closed evidence validation, canonical blocker selection, safe
  Markdown, concise clean output, and the Reporting-owned contract boundary.
- `architecture-health-summary`: Persist the execution context and enforce
  availability/payload conformance for Health report evidence.
- `architecture-change-report`: Persist the execution context in the
  versioned change-report artifact and retain linear resolved-finding
  comparison.

## Impact

Affected code is the Core change-report serializer and comparer, Health
report-evidence projection, Core reporting reader/projector, CLI Markdown
renderer, their tests, reviewed Core public API approval, and the active
OpenSpec artifacts.  The JSON schemas gain required correlation metadata and
the typed reporting boundary moves from `Core.Model` to `Core.Reporting`.
