## ADDED Requirements

### Requirement: Explain reports effective subtractive scope
The explain command SHALL report effective compatible selector scope with authored and imported provenance without requiring clients to parse display prose.

#### Scenario: Explain identifies an exclusion
- **WHEN** an effective scope excludes a resolved item
- **THEN** explain output SHALL identify the exclusion item and its provenance

