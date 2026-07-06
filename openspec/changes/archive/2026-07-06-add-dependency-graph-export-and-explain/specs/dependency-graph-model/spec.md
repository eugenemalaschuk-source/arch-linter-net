## ADDED Requirements

### Requirement: Normalized graph node and edge model
The system SHALL provide a normalized `ArchitectureDependencyGraph` model consisting of nodes (each with a canonical `Id` and a `ArchitectureGraphNodeKind` of `Type`, `Namespace`, `Assembly`, or `External`) and directed edges (each with `SourceId`, `TargetId`, `SourceKind`, `TargetKind`, and a `ContractIds` collection).

#### Scenario: Node identity by kind
- **WHEN** the graph is built at namespace level
- **THEN** every node's `Kind` is `Namespace` and its `Id` is the namespace's full name

#### Scenario: Edge without a violation has empty contract IDs
- **WHEN** an edge represents a first-party reference that does not participate in any contract violation
- **THEN** the edge's `ContractIds` collection is empty

### Requirement: Graph construction is scoped to a single level
The system SHALL build the graph at exactly one selectable level per invocation: `namespace` (default), `type`, or `assembly`. The system SHALL NOT mix nodes of different levels (e.g. `Type` and `Namespace`) in the same graph, except that `External` nodes MAY appear alongside `Namespace` or `Type` nodes.

#### Scenario: Namespace-level graph contains only namespace and external nodes
- **WHEN** the graph is built with level `namespace`
- **THEN** all nodes are `Namespace` kind or `External` kind, and no `Type` or `Assembly` nodes are present

#### Scenario: Assembly-level graph excludes external nodes
- **WHEN** the graph is built with level `assembly`
- **THEN** all nodes are `Assembly` kind and no `External` nodes are present

### Requirement: Deterministic ordering
The system SHALL order graph nodes by `(Kind, Id)` using ordinal string comparison, order edges by `(SourceId, TargetId, SourceKind)` using ordinal string comparison, and order each edge's `ContractIds` ordinally, so that graph output is stable and diffable across runs against unchanged source.

#### Scenario: Repeated builds produce identical output
- **WHEN** the graph is built twice from the same policy and unchanged source code
- **THEN** the resulting node list and edge list (including `ContractIds` order) are identical

### Requirement: Namespace-level graph reuses coverage inventory edges
The system SHALL derive namespace-level edges from the same first-party namespace-to-namespace reference computation used by the architecture coverage inventory, rather than recomputing references independently.

#### Scenario: Namespace edge matches coverage inventory edge
- **WHEN** the coverage inventory reports a dependency edge from namespace A to namespace B
- **THEN** the namespace-level dependency graph contains a corresponding edge from A to B

### Requirement: Type-level graph uses direct reference edges
The system SHALL derive type-level edges from direct type-to-type references (not transitively expanded) when building a `type` level graph.

#### Scenario: Direct type reference produces an edge
- **WHEN** type A directly references type B
- **THEN** the type-level graph contains an edge from A to B

### Requirement: Assembly-level graph uses direct assembly references only
The system SHALL derive assembly-level edges only from direct assembly-to-assembly references (via referenced-assembly metadata) among resolved target assemblies. The system SHALL NOT attempt transitive assembly reference resolution.

#### Scenario: Direct assembly reference produces an edge
- **WHEN** target assembly A directly references target assembly B
- **THEN** the assembly-level graph contains an edge from A to B

#### Scenario: Transitive-only assembly reference produces no edge
- **WHEN** assembly A references assembly B, and B references assembly C, but A does not directly reference C
- **THEN** the assembly-level graph contains no edge from A to C

### Requirement: External nodes represent declared external dependency groups
The system SHALL represent a policy's declared external dependency groups as `External` kind nodes, and SHALL add an edge from a first-party `Namespace` or `Type` node to an `External` node when that first-party node has a reference matching the external group's declared pattern.

#### Scenario: First-party type touching an external group produces an edge
- **WHEN** a first-party type's method body references a member matching the pattern declared for external group `logging-libs`
- **THEN** the graph (at `type` or `namespace` level) contains an edge from that type's/namespace's node to the `logging-libs` `External` node

### Requirement: Violating edges carry contract IDs
The system SHALL tag a graph edge with a contract's ID when that edge's source/target pair corresponds to a violation reported by that contract. For a transitive violation carrying multiple path hops, the system SHALL tag every consecutive-pair edge along each reported path with that contract's ID.

#### Scenario: Direct violation tags the exact edge
- **WHEN** a `strict_dependency` contract with id `no-infra-in-domain` reports a direct violation from namespace A to namespace B
- **THEN** the graph edge from A to B includes `"no-infra-in-domain"` in its `ContractIds`

#### Scenario: Transitive violation tags every hop
- **WHEN** a transitive dependency violation reports path `[A, B, C]` under contract id `no-transitive-infra`
- **THEN** the graph contains edges A→B and B→C, and both include `"no-transitive-infra"` in their `ContractIds`
