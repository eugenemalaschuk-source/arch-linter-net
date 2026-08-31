## Why

Public contract-surface metrics could treat an exported type set as complete even when reflection
could not enumerate or normalize a public member signature. That would publish a lower trusted
value despite the existing measure-first requirement to reject partial evidence.

## What Changes

- Carry member-level reflection completeness through the existing cached public API materialization.
- Return an unassessable public-surface measurement with no partial value or contributors when a
  selected public member cannot be reflected or normalized.
- Restore the established validation topology projection and identity while retaining the stricter
  metric-only projection.

## Capabilities

No capability specification delta is required. `architecture-metric-semantics` already prohibits
partial selected public-surface evidence; this correction ensures the existing requirement includes
member-level reflection failures.

## Impact

- Internal Core public API scanner, topology evaluator, contract executor, and metric evaluator.
- Core regression tests for member-signature completeness and validation identity compatibility.
- No public API, policy syntax, report schema, or normal validation result changes.
