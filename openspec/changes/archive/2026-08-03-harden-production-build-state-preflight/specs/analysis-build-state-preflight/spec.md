## ADDED Requirements

### Requirement: Production validation preserves Platform and runtime identifier
The system SHALL preserve optional Platform and RuntimeIdentifier from public validation and snapshot requests through build-state preflight, output resolution, receipt publication, receipt verification, and evaluated-manifest collection. A requested Platform or RuntimeIdentifier that differs from receipt/output evidence SHALL produce a blocking wrong-context diagnostic and SHALL NOT share a cache-authorization context with another value.

#### Scenario: Validation request selects Platform
- **WHEN** a CLI, Testing, or application-service validation request specifies Platform
- **THEN** the preflight request, manifest, selected output, and emitted receipt contain that same Platform

#### Scenario: Validation request selects runtime identifier
- **WHEN** a CLI, Testing, or application-service validation request specifies RuntimeIdentifier
- **THEN** the preflight request, manifest, selected output, and emitted receipt contain that same RuntimeIdentifier

#### Scenario: Receipt context differs
- **WHEN** a receipt was published for a different requested Platform or RuntimeIdentifier
- **THEN** preflight reports a blocking wrong-context diagnostic before classifying the artifact current
