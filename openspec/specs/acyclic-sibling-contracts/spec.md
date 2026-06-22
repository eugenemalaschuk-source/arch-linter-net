# Acyclic Sibling Contracts Specification

## Purpose
Detects circular reference cycles among sibling namespaces beneath a shared ancestor, independent of any explicit layer ordering.

## Requirements

### Requirement: Detect cycles between sibling namespaces under an ancestor namespace
The system SHALL discover direct child namespaces under one or more configured ancestor namespaces and detect dependency cycles among those sibling groups.

#### Scenario: No cycles in clean sibling graph
- **WHEN** the sibling dependency graph under an ancestor is acyclic
- **THEN** the contract returns an empty cycle list

#### Scenario: Simple 2-node sibling cycle
- **WHEN** sibling namespace `A` depends on sibling namespace `B` and sibling namespace `B` depends on sibling namespace `A`
- **THEN** the contract reports a cycle `"<ancestor>: A -> B -> A"`

#### Scenario: Longer 3+ sibling cycle
- **WHEN** sibling namespaces form a cycle `A -> B -> C -> A` under the ancestor
- **THEN** the contract reports the full cycle path with ancestor prefix

#### Scenario: Descendant dependency attributed to direct sibling
- **WHEN** a type in `Ancestor.A.Sub.X` references a type in `Ancestor.B.Sub.Y`
- **THEN** the dependency is attributed to sibling groups `A` and `B` respectively

### Requirement: Multiple ancestors evaluated independently
The system SHALL evaluate each configured ancestor namespace independently and report cycles per ancestor.

#### Scenario: Two ancestors with independent cycles
- **WHEN** an acyclic sibling contract configures two ancestors, each with sibling cycles
- **THEN** the contract reports both cycles with respective ancestor prefixes

### Requirement: Empty or single-child ancestors produce no cycles
The system SHALL not report cycles when an ancestor has zero or one discovered child namespace.

#### Scenario: Single child namespace under ancestor
- **WHEN** an ancestor namespace contains exactly one child namespace
- **THEN** the contract returns an empty cycle list

#### Scenario: No types match ancestor
- **WHEN** no loaded types match the configured ancestor namespace prefix
- **THEN** the contract returns an empty cycle list

### Requirement: Strict acyclic sibling contracts fail on cycles
A strict acyclic sibling contract SHALL cause validation to fail when cycles are detected.

#### Scenario: Strict contract with cycle fails validation
- **WHEN** a strict acyclic sibling contract detects a cycle
- **THEN** validation reports the cycle and the overall result is failure

### Requirement: Audit acyclic sibling contracts report without failing
An audit acyclic sibling contract SHALL report cycles without causing strict validation to fail.

#### Scenario: Audit contract reports cycle without failure
- **WHEN** an audit acyclic sibling contract detects a cycle
- **THEN** the cycle is reported but does not fail validation

### Requirement: Ignored violations apply to acyclic sibling contracts
The system SHALL apply `ignored_violations` to exclude specific source/reference pairs from the sibling dependency graph.

#### Scenario: Ignored violation breaks sibling cycle
- **WHEN** all edges in a sibling cycle match configured `ignored_violations`
- **THEN** no cycle is reported

### Requirement: Deterministic cycle output
The system SHALL produce deterministic cycle output regardless of type enumeration order.

#### Scenario: Multiple equivalent cycles produce stable output
- **WHEN** a sibling graph contains multiple cycle paths
- **THEN** the reported cycles are in a consistent, ordered format

### Requirement: Acyclic sibling contract accepts optional id
An acyclic sibling contract SHALL accept an optional `id` field. When provided, results SHALL include the contract ID.

#### Scenario: Cycle result includes contract ID
- **WHEN** an acyclic sibling contract with `id: no-sibling-cycles` detects a cycle
- **THEN** the cycle result SHALL include `ContractId == "no-sibling-cycles"`
