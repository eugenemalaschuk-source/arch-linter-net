## ADDED Requirements

### Requirement: Registry publication follows implemented format ownership
The compatibility envelope SHALL distinguish planned format identities from formats shipped by an installed package. A release-matched registry SHALL publish a format only after its owning slice implements the writer and validates a real generated document against the packaged contract.

#### Scenario: Planned format remains unimplemented
- **WHEN** a compatibility blueprint names a future cache, profile, or finding envelope but its owning slice is unfinished
- **THEN** the installed registry omits that envelope and does not claim it as a shipped immutable format
