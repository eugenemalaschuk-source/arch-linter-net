# Proposal

## Why

Qodana currently runs as an advisory scan, so regressions can merge even when its analysis fails or finds new maintainability issues. The repository now has stable scanner evidence and an established Sonar quality gate; Qodana should enforce a reviewed, finding-level policy and provide a useful PR report alongside Sonar.

## What Changes

- Promote Qodana from advisory telemetry to a required PR quality gate.
- Compare findings against a trusted baseline from the exact PR base revision, fail closed on new actionable findings or incomplete analysis, and keep lower-severity diagnostics visible in the report.
- Publish a concise PR report with finding deltas, severity counts, locations, and scanner/evidence status while keeping write-capable reporting separate from untrusted source analysis.
- Refresh the repository's Qodana workflow, adoption decision, required-check inventory, ruleset payload, and quality-gate documentation.
- Preserve the pinned current Qodana Community .NET image and avoid blanket exclusions or unreviewed suppressions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `qodana-community-ci`: replace advisory-only operation with a trusted differential PR gate and actionable report.

## Impact

Changes affect `.github/workflows`, Qodana evidence and reporting scripts, Qodana contract tests, the `Main` ruleset payload, and internal quality-gate documentation. No product API or runtime dependency changes are expected.
