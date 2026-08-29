## MODIFIED Requirements

### Requirement: Structured waiver matching uses canonical finding identity
The system SHALL match a complete structured waiver only when its declared
canonical lowercase fingerprint equals the versioned canonical identity assigned
to a live finding before suppression. It SHALL retain existing legacy glob
matching for entries without structured waiver fields and existing baseline
identity matching for baseline-imported entries. Source-set-expanded aliases of
one authored structured waiver SHALL contribute to that same waiver's matching
state without creating independent declarations.

#### Scenario: Similar display text does not satisfy a structured waiver
- **WHEN** two findings have the same source and forbidden-reference display
  text but different canonical assembly, member, or occurrence identity
- **THEN** a structured waiver fingerprint for one finding SHALL NOT suppress
  the other finding

#### Scenario: Matching expanded alias suppresses its exact finding
- **WHEN** a source-set-expanded alias emits the canonical finding selected by
  an authored structured waiver
- **THEN** that finding is suppressed and the authored waiver is recorded as
  matched even when another alias emits no matching finding
