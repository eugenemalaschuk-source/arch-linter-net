## ADDED Requirements

### Requirement: Type placement supports subtractive matching
The system SHALL allow a type-placement contract to declare compatible include and exclude type matchers, evaluate `union(includes) - union(excludes)` deterministically, and preserve the legacy `types_matching` form unchanged.

#### Scenario: Excluded matching type is not evaluated
- **WHEN** a type matches both a placement include and exclude matcher
- **THEN** the contract SHALL not evaluate or report that type

