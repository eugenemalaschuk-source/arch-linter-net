## ADDED Requirements

### Requirement: Testing API policy-check parity
The Testing API SHALL expose the same policy-only completed, deferred, and typed-failure semantics as the CLI without requiring assemblies or project evaluation.

#### Scenario: Testing API validates a decomposed policy
- **WHEN** a test invokes policy-only validation for a valid decomposed policy
- **THEN** it receives completed checks and deferred records equivalent to CLI machine-readable output
