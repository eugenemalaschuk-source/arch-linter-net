## ADDED Requirements

### Requirement: Semantic coverage evaluates source-relative contextual selectors
Semantic-role coverage SHALL evaluate a contextual target or exclusion selector using a classified source fact when that selector uses a source-relative metadata operator, and SHALL treat the selector as governed or stale based on whether any compatible source-target fact pair matches.

#### Scenario: Not-equal-to-source selector governs a compatible target
- **WHEN** a contextual contract has a source with `metadata: { domain: Sales }` and a target selector with `metadata: { domain: "!{source.metadata.domain}" }`
- **AND** a classified Inventory target exists
- **THEN** semantic coverage treats the Inventory target selector as governed
- **AND** does not report that selector as stale

### Requirement: Semantic coverage deduplicates contextual selectors structurally
Semantic-role coverage SHALL retain distinct contextual selector constraints when deduplicating registered consumers, including differing sequence members of an `in` operator.

#### Scenario: Distinct in selectors remain independent consumers
- **WHEN** contextual selectors contain `metadata: { domain: [Sales] }` and `metadata: { domain: [Inventory] }`
- **THEN** semantic coverage evaluates both selectors independently
