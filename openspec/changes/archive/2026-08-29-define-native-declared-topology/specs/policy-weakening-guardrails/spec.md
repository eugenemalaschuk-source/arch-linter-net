## ADDED Requirements

### Requirement: Reviewed topology scope broadening remains visible to generic weakening comparison
The existing policy-weakening comparison SHALL consume typed topology context
facts and expose a normalized finding when current policy adds a reviewed
topology out-of-scope declaration or makes a same-identity exclusion broader
where direction is statically decidable. It SHALL use existing generic
comparison/provenance/formatter semantics, not a topology-specific weakening
engine; a selector change whose containment cannot be proven SHALL retain
bounded impact-not-proven semantics.

#### Scenario: New topology exclusion is visible
- **WHEN** current policy adds a reasoned reviewed out-of-scope topology declaration absent from base policy
- **THEN** comparison emits deterministic weakening evidence with base/current typed provenance
