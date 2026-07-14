## ADDED Requirements

### Requirement: Policy exception locations use established machine schemas
The CLI SHALL serialize policy-exception JSON locations with the same
snake_case fields as ordinary CI diagnostics. SARIF policy exceptions SHALL
include typed related policy locations in addition to the primary location.

#### Scenario: Conflict has two policy declarations
- **WHEN** a typed policy conflict has root and fragment locations
- **THEN** JSON uses normalized location fields and SARIF contains the fragment
  as a related physical location
