## Why

A public-surface metric refers to a contract by ID but has no strict/audit mode.
The current lookup is case-sensitive and silently selects the strict contract
when a strict and audit contract share an ID, so a report can measure a scope
different from the policy author's intent.

## What Changes

- Resolve public-surface metric target IDs with the repository's normal
  case-insensitive contract-ID semantics.
- Reject a metric target when the same public-surface ID exists in both strict
  and audit collections, rather than choosing one by enumeration order.
- Fail closed in the evaluator as a defensive measure when an unvalidated
  in-memory document contains such an ambiguous target.
- Preserve the existing best-effort public-surface scanner output while making
  swallowed type-name reflection failures explicit completeness evidence for
  metric capture.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-metric-measurement`: public-surface metric target resolution
  becomes case-insensitive and requires one unambiguous strict-or-audit
  contract.

## Impact

Core metric policy validation, metric evaluation, public-surface completeness
tracking, and focused Core tests. No public API, YAML syntax, or report schema
changes.
