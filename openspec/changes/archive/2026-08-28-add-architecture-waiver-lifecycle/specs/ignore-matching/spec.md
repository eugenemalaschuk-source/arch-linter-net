## ADDED Requirements

### Requirement: Structured waiver matching uses canonical finding identity
The system SHALL match a complete structured waiver only when its declared
target fingerprint equals the versioned canonical identity assigned to a live
finding before suppression. It SHALL retain existing legacy glob matching for
entries without structured waiver fields and existing baseline identity
matching for baseline-imported entries.

#### Scenario: Similar display text does not satisfy a structured waiver
- **WHEN** two findings have the same source and forbidden-reference display
  text but different canonical assembly, member, or occurrence identity
- **THEN** a structured waiver fingerprint for one finding SHALL NOT suppress
  the other finding
