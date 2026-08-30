## MODIFIED Requirements

### Requirement: Declaration drift remains separate native evidence
The evaluator MUST infer declaration drift only when `stale_declarations` is
enabled and topology mapping evidence is complete; it SHALL report a declared
node with no currently mapped subject and
an allowed directional edge with no currently mapped observed relationship as
deterministic declaration-drift evidence. Mapping evidence is complete only
when every in-scope observed subject is exactly mapped or explicitly reviewed
out of scope and an exhaustive `allow_empty: false` universe resolves to at
least one subject; any unmapped or ambiguous subject or missing required
universe SHALL make declaration-drift inference unsupported for that
evaluation.

When mapping evidence is incomplete, the evaluator SHALL retain the
unmapped/ambiguous subject evidence and its applicable reason/provenance, but
SHALL NOT emit stale-node, stale-edge, `stale_declaration` applicability, or
ordinary declaration-drift findings inferred from the incomplete graph. This
preserves stale declarations as a distinct proven fact rather than treating an
unresolved mapping as absence. A stale node or edge SHALL remain distinct from
an unmapped observed subject when drift inference is supported.

#### Scenario: Complete mapping supports stale drift
- **WHEN** stale declarations are enabled and every observed subject is exactly
  mapped or reviewed out of scope, but a declared node has no current mapping
  or a declared allowed edge has no current observed relationship
- **THEN** the output retains deterministic stale-declaration evidence and
  provenance for that node or edge

#### Scenario: Ambiguous node mapping does not imply stale nodes
- **WHEN** stale declarations are enabled and one in-scope observed subject
  matches both declared nodes A and B
- **THEN** the evaluator exposes ambiguous-subject evidence and emits neither
  stale node A nor stale node B from that incomplete mapping

#### Scenario: Incomplete endpoint mapping does not imply stale edge
- **WHEN** stale declarations are enabled and a declared edge's possible
  observed endpoint is unmapped or ambiguous
- **THEN** the evaluator exposes the mapping evidence but emits no stale edge
  inferred through that incomplete endpoint

#### Scenario: Unmapped subject does not coexist with inferred drift
- **WHEN** stale declarations are enabled, one declared node has no exactly
  mapped subject, and another observed subject is unmapped
- **THEN** the output retains unmapped-subject evidence without a
  stale-declaration finding inferred from the incomplete evaluation

#### Scenario: Required empty universe does not imply stale declarations
- **WHEN** stale declarations are enabled for an exhaustive
  `allow_empty: false` topology whose scope resolves to zero subjects
- **THEN** the evaluator exposes unexpected-empty applicability evidence and
  emits no stale declaration inferred from the missing required universe
