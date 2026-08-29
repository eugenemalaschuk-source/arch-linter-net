## Why

The architecture-metric semantics define component counts but do not yet state
which existing dependency fact authority applies to every supported native
topology subject kind. Without that compatibility rule, future report work can
produce different project or assembly metric values from the same input.

## What Changes

- Define a normative metric-by-topology-subject projection matrix.
- Make the type, namespace, assembly, and project authorities and projection
  algorithms explicit for component dependency metrics.
- Make the external dependency-group authority explicit, including the bounded
  type-edge ownership projection for project and assembly topology.
- State configuration-invalid and unassessable cases, with deterministic
  scenarios for project component dependencies and project/assembly external
  dependency groups.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-metric-semantics`: Define complete dependency and
  external-group projection compatibility for native topology subject kinds.

## Impact

- Tightens the design-only semantics consumed by #517–#519.
- Adds no production code, policy schema, CLI/output surface, public API, or
  runtime behavior.
