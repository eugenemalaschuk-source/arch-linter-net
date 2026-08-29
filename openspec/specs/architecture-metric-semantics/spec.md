# architecture-metric-semantics Specification

## Purpose
Define a small, deterministic catalog of architecture-governance metric
semantics whose values and contributors remain trustworthy and explainable.
## Requirements
### Requirement: Architecture metrics use a closed deterministic catalog
The architecture-metrics capability SHALL support only the following initial
metric kinds: distinct outgoing architecture components, distinct incoming
architecture components, distinct external dependency groups, distinct project
or assembly footprint for a logical component, type count in a declared
topology slice, and selected public contract-surface size. A metric definition
SHALL identify one metric kind and its bounded native subject; it SHALL NOT
accept arbitrary formulas, expressions, scripts, general maintainability
scores, cyclomatic complexity, runtime measurements, or test-coverage metrics.

#### Scenario: Unsupported formula is not a metric
- **WHEN** a future policy attempts to configure a user-defined expression or
  formula instead of one supported metric kind
- **THEN** the metric capability rejects the configuration rather than
  evaluating a user-defined calculation

### Requirement: Every metric value is a cardinality of canonical contributors
Every evaluable metric value SHALL equal the cardinality of its defined set of
canonical contributor identities. The capability SHALL de-duplicate repeated
occurrences before counting and SHALL expose contributors in ordinal canonical
identity order. YAML position, display text, runtime enumeration order,
multiple method/body references, repeated metadata rows, and multiple paths to
the same contributor SHALL NOT change the value.

#### Scenario: Repeated direct references count one architecture target
- **WHEN** several methods or members create direct references from subjects in
  component A to subjects in component B
- **THEN** B appears once in A's outgoing-component contributor set and adds
  one to the metric value

### Requirement: Component dependency metrics use mapped direct relations
Outgoing and incoming component metrics SHALL target one native declared
topology node, identified by its effective topology provenance and stable node
ID. The capability SHALL derive a direct component relation by mapping both
ends of an existing direct dependency fact to topology nodes. For an outgoing
metric on node A, the contributor set is the distinct target nodes B of
relations `A -> B`; for an incoming metric on A, it is the distinct source
nodes B of relations `B -> A`. The capability SHALL exclude self-pairs from
both sets, SHALL NOT calculate transitive reachability, and SHALL treat each
direction in a cycle as its own direct relation.

#### Scenario: A two-node cycle has no transitive or self-edge inflation
- **WHEN** direct observed relations exist from A to B and B to A, including
  repeated references within each direction
- **THEN** A's outgoing and incoming contributor sets each contain only B, B's
  corresponding sets each contain only A, and no additional contributor is
  created for the cycle or a self relation

### Requirement: External dependency-group metrics count declared group identities
An external dependency-group metric SHALL target one native declared topology
node and count the distinct declared external dependency-group identities that
have a direct relation from one of that node's mapped subjects. It SHALL reuse
the canonical external-group matching and dependency facts, count each matched
group identity once, and neither count first-party component relations nor
invent an `other` group for an unmatched external reference.

#### Scenario: Repeated use of one external group has one contributor
- **WHEN** multiple direct references from a component's mapped subjects match
  the same declared external dependency group
- **THEN** that group appears once in the contributor set and adds one to the
  external dependency-group metric value

### Requirement: Footprint and topology-slice metrics retain native ownership and type units
A logical-component footprint metric SHALL target one native topology node and
one explicit unit of `project` or `assembly`. It SHALL count the distinct
canonical owning project identities or resolved assembly identities of all
observed topology subjects mapped to that node. A topology-slice type-count
metric SHALL target a node in a `subject_kind: type` topology and count the
distinct canonical first-party type facts mapped to that node. The capability
SHALL NOT mix project and assembly contributors, infer ownership from a simple
name, or introduce an implicit generated/test filter beyond the configured
analysis snapshot and topology universe.

#### Scenario: A type topology slice spanning two projects has a distinct footprint and type count
- **WHEN** a type-topology node maps three distinct canonical type facts whose
  canonical ownership spans two projects and three resolved assemblies
- **THEN** its type-count value is three, its project-footprint value is two,
  and its assembly-footprint value is three

### Requirement: Public contract-surface size reuses selected observed API facts
A public contract-surface size metric SHALL target one existing public API
surface contract and count the distinct `(resolved assembly identity,
normalized export signature)` pairs in that contract's current selected
observed export set. It SHALL reuse the public API contract's assembly
resolution, selected-surface selector, export normalization, and required
source-of-truth integrity semantics; it SHALL NOT count source declarations,
documentation, raw reflection enumeration, or a separately reimplemented API
surface.

#### Scenario: Identical signatures from distinct assemblies remain distinct contributors
- **WHEN** one public API surface contract governs two resolved assemblies that
  each expose the same normalized signature in the selected observed surface
- **THEN** the surface-size metric contains two contributor pairs and has value
  two

### Requirement: Metric applicability proves the full native counting universe
Every future metric control SHALL use the governance-applicability evidence
model. A metric is evaluable only when its definition, bounded target, native
counting universe, required measurement facts, and contributor
classifications are complete. Component-relation metrics SHALL be
unassessable when a direct relation needed by the selected component has an
unmapped or ambiguous required component endpoint; explicitly reviewed
out-of-scope endpoints are not component contributors but remain native scope
evidence. Footprint/type metrics SHALL be unassessable when required owner or
type facts are absent or ambiguous. Public surface size SHALL be unassessable
when a governed assembly, selected surface fact, or required public-API source
of truth cannot be resolved. The capability SHALL reuse the analysis
snapshot's canonical project/assembly binding and SHALL not merge or
arbitrarily choose multi-targeted build outputs.

#### Scenario: An unmapped dependency endpoint cannot lower an outgoing count
- **WHEN** component A has one mapped direct target B and one direct target
  that is neither mapped to exactly one component nor explicitly reviewed out
  of scope
- **THEN** A's outgoing-component metric is unassessable with native
  applicability evidence rather than reporting a trustworthy value of one

#### Scenario: A complete empty metric is distinguishable from incomplete input
- **WHEN** a component has no direct external-group relation and all required
  dependency and source facts are complete
- **THEN** its external dependency-group metric is evaluable with value zero
- **AND** a missing, unexpectedly empty, stale, unmapped, ambiguous, or
  unresolved required input is represented as unassessable rather than zero
