## MODIFIED Requirements

### Requirement: Detect directed cycles among layers
The system SHALL build a directed graph of inter-layer references and report every simple directed cycle in that graph. For each cycle, the result SHALL include a path starting at each distinct layer in that cycle. Cycle paths SHALL be returned in deterministic ascending string order.

#### Scenario: No cycles
- **WHEN** the layer dependency graph is acyclic
- **THEN** the contract returns an empty cycle list

#### Scenario: Simple cycle
- **WHEN** layer A depends on layer B and layer B depends on layer A
- **THEN** the contract returns both cycle paths `"A -> B -> A"` and `"B -> A -> B"`

#### Scenario: Complex cycle
- **WHEN** layers form a cycle `A -> B -> C -> A`
- **THEN** the contract returns all three rotations `"A -> B -> C -> A"`, `"B -> C -> A -> B"`, and `"C -> A -> B -> C"`

#### Scenario: Disconnected graph
- **WHEN** some layers have no inter-layer references
- **THEN** only actual cycles are reported; disconnected layers produce no output

#### Scenario: Multiple cycles are ordered deterministically
- **WHEN** the graph contains cycles in multiple strongly connected components
- **THEN** all cycle paths are returned in ascending string order, regardless of graph insertion order

### Requirement: Cycle detection respects ignored violations
The system SHALL exclude references matching `ignored_violations` before building the dependency graph.

#### Scenario: Ignored reference breaks cycle
- **WHEN** a cycle exists but all edges in the cycle match `ignored_violations`
- **THEN** no cycle is reported

### Requirement: Cycle contract accepts optional id
A cycle contract SHALL accept an optional `id` field. When provided, cycle results from this contract SHALL include the contract ID.

#### Scenario: Cycle result includes contract ID
- **WHEN** a cycle contract with `id: no-cycles` detects a cycle
- **THEN** the cycle result SHALL have `ContractId == "no-cycles"`

#### Scenario: Cycle result without explicit ID
- **WHEN** a cycle contract without explicit `id` detects a cycle
- **THEN** the cycle result SHALL have `ContractId` set to the fallback ID derived from `name`
