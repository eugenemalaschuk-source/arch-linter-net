## Why

`ArchitectureMetricEvaluator` mixes metric selection and applicability/output assembly with
separate algorithms for topology, external-dependency, and public-contract-surface metrics. The
topology observation seam introduced by #773 now permits those metric-kind responsibilities to
be named without changing the single measurement authority.

## What Changes

- Extract internal, purpose-named calculators for topology metrics, external dependency groups,
  and public contract-surface metrics.
- Retain `ArchitectureMetricEvaluator` as the sole coordinator for selected definitions,
  applicability completion, contributor normalization, and immutable measurement outcomes.
- Consume the existing canonical metric topology observation and the session-owned public API
  scanner/facts without introducing a second graph, assembly load, source scan, or metric session.

## Capabilities

No externally observable capability or contract changes. This is an internal refactor that
preserves metric formulas, contributor identities, applicability, budgets, policy schema, output,
and public API behavior.

## Impact

Core Execution metric evaluation and focused Core metric tests, plus the active
`decompose-god-classes` architecture-cleanup design and task evidence. No dependencies, public
API snapshots, or policy documents are expected to change.
