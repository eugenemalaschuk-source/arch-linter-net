## Context

Scalar strings in policy context do not always represent exact identities.
Base-type prefixes, asmdef prefixes, forbidden-call/API patterns, namespace
patterns, and project/layer/assembly location allowances match predicates or
combine through a union boundary.

## Decisions

- Retain semantic `Except()` comparison only for an explicit table of
  exact-identity string sets.
- Treat prefix, glob, path-pattern, and matcher strings as unsupported
  directionally until a dedicated containment comparator exists.
- Treat cross-field location allowance/denial facts as unsupported until
  context-bound effective membership proves the union effect; in particular,
  adding a project allowance is not semantic merely because its text changed.
- Emit the existing canonical `typed_fact_impact_not_proven` finding for each
  changed bounded fact shape.

## Non-Goals

- Implement predicate containment or project-to-assembly membership resolution
  in the policy-context comparator.
- Infer effective union membership from raw policy strings or validation state.
