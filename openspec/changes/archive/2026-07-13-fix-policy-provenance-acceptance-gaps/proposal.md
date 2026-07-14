## Why

Policy provenance is currently captured for effective-schema failures and
deserialized contracts, but two execution paths drop it before users can act
on it: CLI error handling renders only exception text, and layer-template
expansion replaces the deserialized template with an unbound synthetic
contract. Conflict-related locations also need a stable composed encounter
order and original-declaration primary semantics.

## What Changes

- Render typed policy diagnostics from import and validation exceptions in the
  CLI's human, JSON, and SARIF output paths.
- Preserve source-template ownership when strict and audit layer templates are
  expanded for execution and policy-consistency checks.
- Preserve composed encounter order for related policy locations and represent
  the original declaration as the primary conflict location.
- Add regression coverage for imported malformed values and expanded imported
  layer templates across reporting and Testing adapter entry points.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-import-composition`: provenance must remain available through CLI
  error reporting and synthetic layer-template expansion, with ordered
  primary/related conflict locations.
- `violation-reporting`: format-aware outputs must represent typed policy
  loading and validation diagnostics.

## Impact

Affected areas are policy provenance binding, layer-template expansion and
catalog execution, CLI exception formatting, JSON/SARIF reporting, and Core/
CLI integration tests. No import grammar, composition semantics, or contract
DTO schema changes are introduced.
