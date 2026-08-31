## ADDED Requirements

### Requirement: Test adapter exposes the canonical policy inventory
`ArchitectureValidationResult` SHALL expose the optional canonical effective
policy inventory from its Core validation outcome without recomputing control
or waiver counts. A result with no inventory evidence SHALL retain that absence
rather than substituting zero counts.

#### Scenario: Test asserts canonical waiver-debt state
- **WHEN** `ValidateStrict()` produces a result with a structured active waiver
- **THEN** a test can assert the same inventory effective-rule and active-waiver
  counts that Core and CLI project
