## Why

The architecture-policy badge and architecture-coverage PR report must be
available to any ArchLinterNet CLI user, not only through repository-specific
Python helpers. The CLI should produce the standard Shields endpoint payload
and the Markdown coverage reports directly from the corresponding validation
results.

## What Changes

- Add native CLI commands that generate deterministic Shields endpoint JSON for
  strict architecture-policy status and full/compact architecture-coverage
  Markdown reports from strict JSON results.
- Preserve strict-validation exit codes for the badge command while still
  emitting a red payload for policy failures and an unavailable payload for
  execution errors.
- Move changed-file coverage classification, failed-rule diagnostic grouping,
  full report, and compact PR-comment report rendering from Python into C#.
- Make the repository workflows and Make targets consume only the native CLI
  commands, replacing the Python report generator and its tests with NUnit
  coverage.
- Document the reusable CLI commands, endpoint payload, and CI integration.

## Capabilities

### New Capabilities

- `architecture-policy-badge-cli`: Native CLI generation of a Shields-compatible
  architecture-policy badge payload and architecture-coverage Markdown reports.

### Modified Capabilities

- `architecture-policy-badge`: The repository badge and coverage comment are
  generated through native CLI commands rather than repository-only glue.
- `architecture-coverage-ci-reporting`: Coverage-report rendering moves from
  Python scripts to the standard CLI.
- `github-actions-ci`: The dedicated workflow invokes the native badge command
  as its strict-policy gate.

## Impact

Affected areas are the CLI command catalog, report-rendering implementation,
CLI tests, CI workflows, Make targets, documentation, and OpenSpec. The
existing GitHub workflow-status image remains the public README image; no
Python generator or CI content write is introduced.
