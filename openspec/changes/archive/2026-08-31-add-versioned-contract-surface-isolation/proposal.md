## Why

The general contract-surface exposure rules can identify a forbidden type in a
visible signature, but authoring a version boundary currently requires
duplicating selectors in every rule. A dedicated versioned-surface isolation
capability makes those boundaries declarative, reviewable, and fail-closed
while reusing the existing exposure evidence and governance lifecycle.

## What Changes

- Add strict and audit policy controls for versioned contract-surface
  isolation.
- Allow policies to declare named, bounded version/surface groups through the
  existing structural and semantic selectors, then reference those groups as
  the source and forbidden targets of an isolation rule.
- Resolve the groups deterministically and reuse the existing recursive
  contract-surface exposure evaluator, findings, applicability evidence,
  baseline identity, and output projection.
- Reject invalid, duplicate, unknown, empty, or self-referential group/rule
  declarations; report unexpected zero-match groups as unassessable rather
  than clean.
- Document static contract-surface isolation and its boundary with runtime API
  version negotiation, payload compatibility, and binary compatibility.

## Capabilities

### New Capabilities

- `versioned-contract-surface-isolation`: Declarative rules that prevent one
  named, statically selected contract surface from exposing types in another
  named version or forbidden implementation surface.

### Modified Capabilities

- None.

## Impact

The Core policy model, raw/schema and typed validators, family registration,
contract checker integration, baseline/normalized output plumbing, Core tests,
and contract documentation gain a new additive family. Existing generic
contract-surface exposure rules and their public API remain unchanged.
