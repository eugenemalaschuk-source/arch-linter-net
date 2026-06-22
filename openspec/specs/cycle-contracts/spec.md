# Cycle Contracts Specification

## Purpose
Detects directed dependency cycles among declared layers, respecting ignored violations.

## Requirements

### Requirement: Detect directed cycles among layers
The system SHALL build a directed graph of inter-layer references and detect all cycles using DFS.

#### Scenario: No cycles
- **WHEN** the layer dependency graph is acyclic
- **THEN** the contract returns an empty cycle list

#### Scenario: Simple cycle
- **WHEN** layer A depends on layer B and layer B depends on layer A
- **THEN** the contract returns a cycle path string `"A -> B -> A"`

#### Scenario: Complex cycle
- **WHEN** layers form a cycle `A -> B -> C -> A`
- **THEN** the contract returns the cycle path `"A -> B -> C -> A"`

#### Scenario: Disconnected graph
- **WHEN** some layers have no inter-layer references
- **THEN** only actual cycles are reported; disconnected layers produce no output

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
