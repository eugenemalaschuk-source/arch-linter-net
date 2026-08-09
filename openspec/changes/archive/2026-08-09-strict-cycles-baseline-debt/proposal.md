## Why

`strict_cycles` currently feeds every graph edge inspected during analysis into baseline generation,
including edges from acyclic graphs. This can persist non-findings as accepted debt and makes baseline
verification report contradictory new-count, sync, and exit-code results.

## What Changes

- Restrict strict-cycle baseline candidates to concrete graph edges that participate in an actual
  detected cycle.
- Preserve existing cycle detection and deterministic ordering while retaining exact, structured
  identity for eligible cycle evidence.
- Make baseline verification treat any new candidate as out of sync, consistently across human
  output, JSON output, and the process exit code.
- Add acyclic and cyclic multi-layer regression coverage for baseline update and verification.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `adoption-stabilization-compatibility`: baseline lifecycle safety and reviewability for strict-cycle findings.

## Impact

The change affects Core cycle analysis and baseline lifecycle classification, with focused Core and CLI
tests. It does not change policy schema, public commands, or non-cycle baseline identity families.
