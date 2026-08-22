## Why

The initial release-forensics report implementation does not yet enforce its
byte-canonical JSON promise at the process stdout boundary and permits malformed
UTF-16 input to escape the canonical writer. Repository lint also rejects two
new private static field names. These gaps make schema version 1 unsafe to
freeze despite the rest of the Git-only analysis contract being complete.

## What Changes

- Enforce Unicode-scalar validation for every string rendered by canonical JSON
  and produce a deterministic, separate failure when invalid surrogate content
  reaches report rendering.
- Emit JSON reports through an explicit UTF-8-without-BOM stdout byte boundary;
  Markdown and diagnostic output retain their existing console behavior.
- Add byte-level regression coverage for non-ASCII report output, surrogate
  rejection, enrichment ordering/states, and report-versus-diagnostic
  separation.
- Correct the private static field names that fail repository formatting rules.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-forensics-reporting`: Require scalar-valid canonical JSON rendering,
  deterministic rendering failure behavior, and byte-level contract coverage.
- `release-forensics-history-cli`: Require JSON report stdout to preserve its
  UTF-8-without-BOM canonical byte representation.

## Impact

Affected areas are the Core canonical JSON/report renderers, the CLI console and
history command boundary, focused Core/CLI tests, and the release-forensics
OpenSpec contracts. No Git ingestion, scoring, graph, or enrichment-provider
behavior changes.
