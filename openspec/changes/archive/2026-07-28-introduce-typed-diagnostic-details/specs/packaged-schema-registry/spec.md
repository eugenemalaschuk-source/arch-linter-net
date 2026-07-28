## ADDED Requirements

### Requirement: Packaged normalized finding schema
The immutable packaged schema registry SHALL publish the implemented versioned normalized diagnostic JSON schema only after generated JSON output validates against it.

#### Scenario: Offline schema validates generated diagnostics
- **WHEN** an installed tool lists and prints the normalized diagnostic schema offline
- **THEN** the exact packaged schema declares the supported finding schema version and validates generated diagnostic JSON
