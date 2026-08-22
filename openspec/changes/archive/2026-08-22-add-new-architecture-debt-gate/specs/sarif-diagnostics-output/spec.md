## ADDED Requirements

### Requirement: SARIF projects normalized architecture-debt gate results
SARIF output for the architecture-debt gate SHALL emit deterministic results for persistent-debt lifecycle entries and policy-weakening findings with a typed section discriminator. Persistent results SHALL retain canonical identity and lifecycle status; weakening results SHALL retain weakening identity, classification, severity, values, provenance, and rationale without baseline lifecycle fields.

#### Scenario: Gate SARIF preserves independent categories
- **WHEN** a gate result includes a new persistent finding and a warning policy-weakening finding
- **THEN** SARIF contains one deterministic result for each with distinct section properties and rule namespaces
