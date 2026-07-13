## ADDED Requirements

### Requirement: Semantic coverage preserves contextual selector semantics
Semantic-role coverage SHALL evaluate contextual consumer evidence using the complete registered selector, including all metadata values and selector operators, when deciding whether a classification fact is governed and whether a contextual consumer is stale.

#### Scenario: Contextual metadata value does not govern another value
- **WHEN** a contextual selector declares `role: DomainLayer` and `metadata: { domain: Sales }`
- **AND** a classified type has `role: DomainLayer` and `metadata: { domain: Inventory }`
- **THEN** semantic coverage reports the Inventory fact as uncovered unless another layer or consumer governs it
- **AND** it reports the Sales consumer as stale when no Sales fact exists

#### Scenario: External selector layer does not become stale evidence
- **WHEN** a selector-backed layer is marked `external: true` and matches no discovered type
- **THEN** semantic coverage does not emit stale evidence for that layer
