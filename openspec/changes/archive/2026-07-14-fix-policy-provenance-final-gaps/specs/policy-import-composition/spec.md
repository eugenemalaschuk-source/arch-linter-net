## ADDED Requirements

### Requirement: Provenance order follows effective encounter order
The system SHALL order primary and related policy locations by effective-node encounter order, including nodes from the same source document.

#### Scenario: Double-digit sequence indices
- **WHEN** locations from one source include sequence indices 2 and 10
- **THEN** the output preserves their composed order rather than lexical path order

### Requirement: Template expansion failures are typed policy diagnostics
The system SHALL enrich invalid imported exhaustive layer-template expansion failures with the source template provenance before CLI reporting.

#### Scenario: Dotted exhaustive template layer
- **WHEN** an imported exhaustive template declares a dotted layer name
- **THEN** JSON and SARIF report the typed fragment template location
