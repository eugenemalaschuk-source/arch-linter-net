## MODIFIED Requirements

### Requirement: Reviewed topology scope broadening remains visible to generic weakening comparison
The existing policy-weakening comparison SHALL consume typed topology context
facts and expose a normalized finding when current policy adds a reviewed
topology out-of-scope declaration or makes a same-identity exclusion broader
where direction is statically decidable. It SHALL compare topology selectors
structurally, so any changed role, metadata key/value pair, namespace suffix,
or CEL predicate remains visible even if its text contains delimiter
characters. It SHALL use existing generic comparison/provenance/formatter
semantics, not a topology-specific weakening engine; a selector change whose
containment cannot be proven SHALL retain bounded impact-not-proven semantics.

#### Scenario: New topology exclusion is visible
- **WHEN** current policy adds a reasoned reviewed out-of-scope topology declaration absent from base policy
- **THEN** comparison emits deterministic weakening evidence with base/current typed provenance

#### Scenario: Structurally changed same-ID exclusion remains visible
- **WHEN** a same-ID reviewed topology exclusion changes only metadata values that collide under delimiter serialization
- **THEN** comparison emits the bounded changed-selector evidence instead of treating the exclusions as equal
