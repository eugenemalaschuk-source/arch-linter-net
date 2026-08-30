# declared-topology-evaluation Specification

## Purpose
Define deterministic evaluation of observed first-party dependencies against a
policy's declared topology, including mapping completeness, directional
relationships, declaration drift, and shared applicability evidence.
## Requirements
### Requirement: Declared topology evaluates one canonical observed universe
When an effective policy declares a topology, the system SHALL derive the
declared `subject_kind`'s observed first-party subject universe and dependency
facts from the current analysis session. Each subject identity SHALL retain its
kind, canonical project, canonical assembly, and subject identity where
applicable, and every collection SHALL use deterministic semantic ordering.
Policies without a topology SHALL retain their existing validation behavior.

#### Scenario: Same-named namespace subjects have separate owners
- **WHEN** two selected first-party assemblies expose the same namespace name
- **THEN** topology evaluation retains them as distinct canonical namespace subjects when their owning assembly or project differs

#### Scenario: Reordered analysis facts are equivalent
- **WHEN** the same observed subjects and dependencies are supplied in different enumeration orders
- **THEN** the topology evidence, findings, counts, and selected witnesses are canonically equivalent

### Requirement: Mapping classifications preserve reviewed scope and completeness semantics
The evaluator SHALL first select the declared scope, then classify every
in-scope observed subject as reviewed out of scope when it matches a reviewed
exclusion, mapped when exactly one node matches, unmapped when no node matches,
or ambiguous when multiple nodes match. It SHALL retain each material
unmapped/ambiguous subject and relevant selector/node evidence in canonical
native topology evidence. YAML declaration order SHALL NOT alter a
classification.

#### Scenario: Reviewed exclusion precedes node mappings
- **WHEN** an in-scope subject matches both a named reviewed exclusion and a node mapping
- **THEN** it is recorded as reviewed out of scope and is neither mapped nor unmapped

#### Scenario: Exhaustive topology has one unmapped subject
- **WHEN** one in-scope observed subject matches no node or reviewed exclusion in an exhaustive topology
- **THEN** its topology applicability record is unassessable with `unmapped_subject` evidence and the subject remains available for bounded drill-down

#### Scenario: Partial topology retains an undeclared subject
- **WHEN** a partial topology has an in-scope subject matching no node
- **THEN** the subject is retained as unmapped native evidence but does not alone make the topology applicability record unassessable

#### Scenario: Multiple node matches stay ambiguous
- **WHEN** an in-scope observed subject matches mappings for more than one node
- **THEN** the evaluator reports deterministic ambiguous-subject structural evidence and unassessable applicability evidence without selecting a node by declaration order

### Requirement: Required topology evidence fails closed
An exhaustive topology SHALL create one required applicability control using the
existing canonical expected-record and completion model. An empty exhaustive
subject universe with `allow_empty: false`, an unmapped subject, an ambiguous
subject, or an enabled stale declaration SHALL produce existing typed
unassessable reason/provenance evidence. A partial topology SHALL remain
explicitly visible without converting a merely unmapped subject into an
exhaustive completeness failure. A policy without topology SHALL not create an
applicability control.

#### Scenario: Empty exhaustive universe is not clean
- **WHEN** an exhaustive topology observes zero scoped subjects and `allow_empty` is false
- **THEN** the record is unassessable with `unexpected_empty_input` rather than a clean zero-finding assessment

#### Scenario: Explicitly allowed empty universe is evaluable
- **WHEN** an exhaustive topology observes zero scoped subjects and `allow_empty` is true with no other insufficient evidence
- **THEN** it produces evaluable topology evidence with zero observed subjects

### Requirement: Observed component dependencies respect declared direction
The evaluator SHALL consider only dependencies whose source and target subjects
are exactly mapped, ignore intra-component dependencies, and group all other
dependencies by directed mapped-node pair. For each observed component pair not
listed in `allowed_edges`, it SHALL emit one deterministic relational finding
with source node, target node, and a representative canonical subject-level
dependency witness. Mapping/applicability gaps SHALL remain distinct from these
ordinary forbidden-edge findings.

#### Scenario: Forbidden relationship has a stable witness
- **WHEN** several observed dependencies map from component `application` to component `persistence` and that direction is undeclared
- **THEN** exactly one relational finding identifies `application`, `persistence`, and the canonical first dependency witness regardless of discovery order

#### Scenario: Correctly mapped allowed relationship is clean
- **WHEN** an observed dependency maps to a declared allowed component direction
- **THEN** the evaluator retains the relationship as native evidence and emits no forbidden-edge finding for it

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

### Requirement: Native topology evidence reuses shared result and output seams
Topology evaluation SHALL retain its canonical subject, mapping, relationship,
and drift evidence as typed family evidence on the existing applicability
record. The existing completion, normalized finding, cache, Human, JSON, SARIF,
Testing, and baseline seams SHALL project equivalent control identity, reason,
provenance, and typed evidence without topology YAML reparsing or independent
source/assembly recounting. The evidence counts SHALL be presented as
completeness evidence and SHALL NOT be a topology quality score.

#### Scenario: Normalized consumers preserve topology unassessability
- **WHEN** an exhaustive topology has an ambiguous subject
- **THEN** Human, JSON, SARIF, and Testing expose the same canonical applicability identity, `ambiguous_subject` reason, provenance, and topology evidence through the existing shared projection

#### Scenario: Downstream reporting consumes canonical mapping counts
- **WHEN** topology evaluation completes with mapped, unmapped, or ambiguous subjects
- **THEN** a downstream consumer can obtain declared-component and canonical mapping counts and bounded subject evidence from the result without parsing policy YAML or scanning assemblies
