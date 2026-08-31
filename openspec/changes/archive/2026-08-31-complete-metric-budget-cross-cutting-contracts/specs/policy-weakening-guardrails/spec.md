## ADDED Requirements

### Requirement: Relaxed metric-budget bounds are semantic weakening
The policy-weakening comparer SHALL report semantic weakening when a retained
metric-budget contract's maximum increases or minimum decreases. It SHALL use
the typed budget facts from effective policy contexts and preserve ordinary
deterministic control identity, evidence, and provenance.

#### Scenario: An upper bound is relaxed
- **WHEN** a retained strict metric budget changes its maximum from 10 to 20
- **THEN** comparison reports a semantic weakening for that budget

#### Scenario: A lower bound is relaxed
- **WHEN** a retained strict metric budget changes its minimum from 10 to 5
- **THEN** comparison reports a semantic weakening for that budget
