## Context

See [proposal.md](proposal.md). Public API materialization is session-cached and feeds both legacy
validation and measure-first public-surface capture. Member reflection has historically been
best-effort: unavailable members are omitted rather than failing validation.

## Goals / Non-Goals

**Goals:**

- Retain an aggregate completeness bit for every exported member enumeration and signature render.
- Make public-surface metric capture fail closed without rescanning or changing its value semantics.
- Keep validation's existing topology projection, findings, and SARIF identity stable.

**Non-Goals:**

- Turn member reflection failures into legacy validation findings.
- Change public API snapshot format, policy syntax, or any public API.

## Decisions

### Accumulate member scan completeness in the cached materialization

The scanner creates one mutable internal accumulator seeded from type-universe completeness.
Best-effort member enumeration and signature normalization retain their existing omission behavior,
but record incomplete evidence before returning. The cached materialization then exposes the final
boolean to the existing metric capture seam. This preserves one traversal and lets validation
continue consuming the same best-effort entries.

### Separate topology projections at the caller boundary

The metric evaluator keeps the strict canonical/metadata projection. The normal contract executor
explicitly selects a legacy type-derived projection, including its previous identity grammar. This
avoids making `validate` output depend on the metrics feature while keeping metric ownership and
endpoint binding precise.

## Risks / Trade-offs

- [A harmless omitted member suppresses a metric] → metric semantics prohibit asserting a complete
  selected public surface without the missing reflection evidence, so fail-closed is required.
- [Projection implementations drift] → isolate the split at explicit evaluator entry points and
  cover both the metric failure and validation identity compatibility.

## Migration Plan

1. Thread member completeness through the scanner cache and metric capture.
2. Direct ordinary validation to its preserved projection.
3. Verify focused Core tests and repository quality gates; rollback is a normal code revert.
