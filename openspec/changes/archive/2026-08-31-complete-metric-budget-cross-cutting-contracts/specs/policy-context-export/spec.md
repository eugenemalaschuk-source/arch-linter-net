## ADDED Requirements

### Requirement: Policy context exports typed metric-budget contract facts
Policy-context export SHALL support metric-budget contracts in the same closed
contract-type coverage set as every registered contract family. Its
deterministic JSON and Markdown representations SHALL include the budget's
metric ID and configured minimum and maximum facts, when present.

#### Scenario: Budget facts survive policy-context export
- **WHEN** a composed policy contains an audit metric budget with both bounds
- **THEN** JSON and Markdown policy context expose its metric, minimum, and
  maximum facts with ordinary contract identity and provenance
