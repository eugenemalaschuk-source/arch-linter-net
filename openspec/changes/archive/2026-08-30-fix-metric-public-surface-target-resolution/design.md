## Context

See [proposal.md](proposal.md). Public API contracts intentionally allow a
strict and audit contract to share an ID, but a metric selector has no mode
field. Contract identifiers elsewhere in the policy are case-insensitive.

## Goals / Non-Goals

**Goals:**

- Give each public-surface metric one deterministic, policy-author-visible
  target.
- Preserve fail-closed measurement semantics for callers that build a document
  without running policy validation.
- Record unavailable type-name metadata as incomplete public-surface evidence.

**Non-Goals:**

- Add a mode field or alter strict/audit contract execution.
- Change legacy validation or snapshot output.

## Decisions

### Reject cross-mode IDs only for metric targets

Strict and audit public-surface contracts may still share an ID for validation.
When a metric names that ID, policy validation rejects it because there is no
unambiguous surface to measure. This avoids inventing strict precedence or a
new configuration field.

### Use ordinal case-insensitive target matching

Metric references follow the repository-wide contract-ID comparison rule. The
evaluator applies the same comparison and requires exactly one match as a
defence against programmatically constructed, unvalidated documents.

### Add an observable type-name rendering result

The scanner needs to distinguish an empty name from a `TypeLoadException` or
`FileNotFoundException` suppressed for legacy behavior. A non-public helper
returns success alongside the normalized name; metric materialization marks its
existing completeness accumulator incomplete when rendering fails.

## Risks / Trade-offs

- [A valid-looking legacy scan has unavailable metadata] -> Legacy output is
  unchanged; only metrics become unassessable rather than publish a partial
  count.
- [A policy used the same ID in both modes for a metric] -> It receives an
  explicit configuration error and must use distinct IDs.

## Migration Plan

1. Add validation and evaluator guards with regression tests.
2. Propagate type-name rendering failures to metric completeness.
3. Run focused Core tests and OpenSpec validation; rollback is a normal revert.
