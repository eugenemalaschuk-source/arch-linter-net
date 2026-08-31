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

### Requirement: Metric topology-subject projection compatibility is explicit
The metric capability SHALL select dependency and external-group facts only
through the following closed projection matrix. A `map` operation means the
existing native-topology classification of an observed subject to exactly one
node; all existing unmapped, ambiguous, and reviewed-out-of-scope semantics
remain in force. A namespace subject identity SHALL retain its canonical
project and resolved assembly owner; matching the namespace text alone SHALL
NOT bind a fact to a different owner.

| Metric | `type` topology | `namespace` topology | `project` topology | `assembly` topology |
| --- | --- | --- | --- | --- |
| Outgoing/incoming component count | Direct first-party edges from the type-level graph, then map each type endpoint. | Direct first-party edges from the namespace-level graph, then map each namespace endpoint with its canonical owner. | Direct first-party type-level graph edges; replace each endpoint with its canonical owning project identity, then map each project endpoint. | Direct assembly-level graph edges; bind each graph endpoint to exactly one canonical resolved assembly topology subject, then map each endpoint. |
| External dependency-group count | Type-level graph edges from a first-party type to an `External` group, then map the source type. | Namespace-level graph edges from a first-party namespace to an `External` group, then map the source namespace with its canonical owner. | Type-level graph edges to an `External` group; replace the source type with its canonical owning project identity, then map that project. | Type-level graph edges to an `External` group; replace the source type with its canonical resolved assembly identity, then map that assembly. |
| Project/assembly footprint count | Existing mapped type facts and their canonical owner in the requested unit. | Existing mapped namespace facts and their canonical owner in the requested unit. | Existing mapped project facts and their canonical project/assembly owner in the requested unit. | Existing mapped assembly facts and their canonical project/assembly owner in the requested unit. |
| Topology-slice type count | Existing mapped canonical type facts. | Configuration-invalid. | Configuration-invalid. | Configuration-invalid. |
| Public contract-surface size | Not topology-targeted; requires one public API surface contract. | Not topology-targeted; requires one public API surface contract. | Not topology-targeted; requires one public API surface contract. | Not topology-targeted; requires one public API surface contract. |

The project projection SHALL use only direct type-level first-party edges and
canonical project ownership; it SHALL NOT use assembly edges, perform a new
project graph scan, infer transitive edges, or join on display/simple names.
The assembly component projection SHALL use direct assembly graph edges, not
type-edge aggregation. Project and assembly external-group projections SHALL
start only from type-level external edges because the assembly graph has no
external nodes. Every resulting component pair or external-group contributor
SHALL retain the existing set deduplication, ordinal ordering, self-edge, and
cycle semantics.

A metric is configuration-invalid when it lacks the target shape mandated by
this matrix, including component/external metrics without a native topology
node, type count on a non-type topology, and a public surface metric without a
public API surface contract. A matrix-permitted metric is unassessable when a
required graph is absent or incomplete; a type, project, namespace, or assembly
ownership binding is missing or ambiguous; an assembly graph endpoint has zero
or multiple canonical resolved assembly topology subjects; or a required mapped
topology endpoint is unmapped or ambiguous. The capability SHALL NOT substitute
another graph level, choose an arbitrary candidate, join on namespace text
alone, or use a partial known subset in any of those cases.

#### Scenario: Project topology derives one component relation from direct type edges
- **WHEN** direct type-level graph edges from types owned by project P to types
  owned by project Q are repeated, P maps to topology node A, and Q maps to
  topology node B
- **THEN** project-topology outgoing and incoming metrics derive the distinct
  direct component relation `A -> B` by canonical project ownership projection,
  without creating a project graph or using assembly graph edges

#### Scenario: Project topology external groups derive from source-type ownership
- **WHEN** repeated type-level external edges from types canonically owned by
  project P target declared external group G and P maps to topology node A
- **THEN** A's external dependency-group contributor set contains G exactly
  once through the type-edge-to-project projection

#### Scenario: Assembly topology external groups do not use the assembly graph
- **WHEN** a type-level external edge from a type with canonical resolved
  assembly X targets declared external group G, X maps to topology node A, and
  the assembly graph contains no external nodes
- **THEN** A's external dependency-group contributor set contains G through the
  type-edge-to-assembly projection rather than a synthetic assembly external
  edge

#### Scenario: Identical namespace names retain their canonical owners
- **WHEN** two canonical assemblies expose the same namespace text and only one
  owner's type has a matching external dependency fact
- **THEN** a namespace-topology external dependency-group metric attributes the
  group only to that owner's mapped topology node

#### Scenario: Ambiguous assembly binding is unassessable
- **WHEN** an assembly-level graph endpoint has zero or multiple canonical
  resolved assembly topology subjects for its simple name
- **THEN** the affected assembly-topology component dependency metric is
  unassessable and does not select a subject by assembly simple name

### Requirement: Project metric ownership binds one resolved artifact
Project metric projections SHALL bind each resolved artifact to one project.
Project topology, project footprint, and project external-dependency metric
projections SHALL bind every contributing resolved assembly artifact to exactly
one discovered project through its stable normalized project identity. They
SHALL NOT infer a project owner from an output assembly simple name. A metric
project-topology projection SHALL retain that artifact-derived identity when it
classifies subjects and relations, while accepting the existing project selector
spelling only as a policy-facing display selector. When a contributing artifact
has zero or multiple project-owner candidates, when its output assembly simple
name identifies multiple distinct discovered project artifacts, or when a
selected legacy project selector corresponds to more than one artifact-derived
project subject, the affected metric SHALL be unassessable with missing required
input instead of selecting a project by discovery order or merging the subjects.
When an ordinary measurement explicitly selects target assemblies and also
configures project metrics, the analysis snapshot SHALL materialize the
project-output evidence needed to establish those artifact-derived bindings
without requiring build preparation.

#### Scenario: Duplicate output assembly names do not merge project contributors
- **WHEN** two discovered projects have the same output assembly simple name
  and a selected metric requires ownership of an artifact with that name
- **THEN** the metric is unassessable with missing required input and does not
  publish either project as a canonical contributor

#### Scenario: Project relation retains exact artifact ownership before mapping
- **WHEN** two resolved artifacts share an output assembly simple name, their
  discovered project bindings are distinct, and an observed direct edge starts
  from one artifact
- **THEN** the metric projection retains the starting artifact's normalized
  project identity until it has established that the selected project selector
  denotes exactly one projected project subject

#### Scenario: Explicit target assemblies retain unambiguous project ownership
- **WHEN** ordinary measurement explicitly selects a target assembly, configures
  its project, and the selected resolved artifact has exactly one fresh project
  output candidate
- **THEN** a project-unit metric is evaluable with that normalized project path
  as its canonical contributor without requiring build preparation
