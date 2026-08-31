## MODIFIED Requirements

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
