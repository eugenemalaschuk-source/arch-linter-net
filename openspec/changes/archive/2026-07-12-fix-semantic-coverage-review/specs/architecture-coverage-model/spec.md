## ADDED Requirements

### Requirement: Semantic coverage preserves complete layer constraints
Semantic-role coverage SHALL evaluate a selector-backed layer against the concrete type using the same namespace-and-selector matching semantics as ordinary layer resolution.

#### Scenario: Combined layer does not cover a different namespace
- **WHEN** a layer declares both a namespace and a selector and a type matches only the selector
- **THEN** semantic coverage SHALL not classify that type as governed by the layer

### Requirement: Semantic roots use namespace-root syntax
Semantic-role coverage roots, when declared, SHALL use only the existing namespace and optional namespace-suffix matcher shape.

#### Scenario: Invalid semantic root is rejected
- **WHEN** a semantic-role coverage root omits namespace or uses discovery include/exclude fields
- **THEN** policy validation SHALL reject the contract with an actionable error
