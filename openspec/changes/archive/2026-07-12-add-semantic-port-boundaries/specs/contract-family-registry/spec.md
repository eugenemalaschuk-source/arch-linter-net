## ADDED Requirements

### Requirement: The contract-family registry includes port boundaries
The ordered contract-family registry SHALL expose the `port_boundary` family,
its strict and audit YAML group names, checker, owned contract types, and
baseline capability. The catalog and executor SHALL dispatch it using the same
descriptor-driven flow as other baseline-capable families.

#### Scenario: A selected port-boundary contract is executable
- **WHEN** a policy declares a strict or audit port-boundary contract and it is
  selected for execution
- **THEN** the registry SHALL resolve and invoke that family's checker
