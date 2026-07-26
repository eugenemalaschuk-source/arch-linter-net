## ADDED Requirements

### Requirement: Rule-input summaries expose optional-empty evidence
Rule-input coverage summaries SHALL distinguish covered, optional-empty, stale, and unknown inputs. Every optional-empty item SHALL include its exact contract/input/layer identity, reason, and authored provenance in the normalized summary and in human, JSON, SARIF, explain, and Testing API projections.

#### Scenario: Structured projections agree on optional state
- **WHEN** a selected rule-input contract contains an optional empty input
- **THEN** human, JSON, SARIF, explain, and Testing API output expose the same typed optional-empty identity and reason without requiring consumers to parse display text
