## Context

The expected-membership design uses a left join to produced records. That join
must be one-to-zero-or-one to retain a deterministic denominator and numerator.

## Goals / Non-Goals

**Goals:**

- Make record cardinality explicit and fail closed on duplicate records.

**Non-Goals:**

- Change membership, state, family evidence, #685 inventory ownership, or any
  executable product behavior.

## Decisions

### 1. Enforce one-to-zero-or-one join cardinality

An expected control has zero produced records when evaluation failed to
materialize its record and exactly one when it succeeded. More than one record
for the same canonical identity is a contract-integrity failure, not a
collection to aggregate or choose from. The summary keeps the expected control
once in its denominator and marks it unassessable.

Alternative considered: deduplicate records by state or ordering. Rejected
because it would select arbitrary evidence and make evaluator disagreement
appear evaluable.

## Risks / Trade-offs

- [Producer repeats a record] → Preserve the duplicate provenance as
  unassessable evidence rather than double-counting or silently deduplicating.

## Migration Plan

Design-only correction before implementation; no runtime migration is needed.
