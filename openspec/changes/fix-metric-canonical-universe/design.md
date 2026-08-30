## Context

See `proposal.md`. Metrics consume the topology evaluator's session projection,
so their contributor identities and applicability decisions must not collapse or
broaden that projection. The canonical metric specifications already require
this behaviour.

## Goals / Non-Goals

**Goals:**

- Keep each assembly-footprint contributor bound to its resolved canonical
  assembly identity, and each type-count contributor bound to its complete
  canonical topology subject identity.
- Fail closed for an assembly graph endpoint with zero or multiple retained
  same-simple-name subjects.
- Treat an explicitly `allow_empty` topology scope (or empty selected node)
  as a complete zero unless its own stale declaration evidence says otherwise.
- Limit project-ownership checks for external facts to a mapped source that is
  actually selected by the metric.

**Non-Goals:**

- Change policy syntax, measurement output schema, or public APIs.
- Make unrelated unmapped topology subjects invalidate a bounded metric. A
  source that is definitively not mapped to the selected node cannot contribute
  to that node's external-group set.

## Decisions

### Contributor keys are native canonical identities

Assembly footprint uses the observed subject's canonical resolved-assembly
identity. Type count uses the full topology subject identity, which includes
project, simple assembly, canonical assembly, and type identity. This keeps
same-name outputs and same fully-qualified types from collapsing. The existing
ordinal sort in `Finish` remains the single display-order authority.

Using display names would make reports friendlier but is unsafe because they
are not unique; presentation aliases are intentionally not added in this
correction.

### Assembly endpoint cardinality is checked before identity matching

The retained index remains grouped by simple name so a dependency endpoint can
locate its candidate set. It returns ambiguous immediately unless that set has
exactly one subject. Only then can canonical/reference identity verify that the
one candidate really is the observed endpoint. This implements the semantic
requirement that zero or multiple canonical subjects for the simple name do
not select an owner.

### Empty measurement scope is a value only when policy permits it

`topology.scope.allow_empty` already makes the topology projection evaluable
when no subjects are observed. Metrics reuse that authority: if the selected
node has no classifications and no target stale declaration, they finish with
an empty contributor set and numeric zero. Without `allow_empty`, the existing
unexpected-empty result remains.

### External ownership checks follow selected source binding

External facts are first mapped through the exact topology classification.
Project ownership is checked only for a mapped source in the selected node.
Facts outside the bounded target no longer introduce an unrelated
missing-required-input reason; ambiguous sources containing the selected node
remain covered by the metric's existing ambiguity evidence.

## Risks / Trade-offs

- [Canonical contributor strings are longer than display names] → They are the
  required deterministic evidence; bounded output limits continue to contain
  report size.
- [Some formerly trusted measurements become unassessable] → This is the
  intentional fail-closed result when identity cannot select one native owner.
- [Empty-node zero could hide declaration drift] → Target-specific stale
  declaration evidence still produces an unassessable result.
