## MODIFIED Requirements

### Requirement: Unsupported coverage scopes are rejected until their implementations land
Until the dependency-edge coverage family is implemented, the system SHALL reject the `dependency_edge` scope with an actionable error instead of silently accepting it. `project` and `assembly` scopes are implemented by the `project-coverage-contracts` and `assembly-coverage-contracts` capabilities and SHALL be accepted.

#### Scenario: A dependency-edge coverage contract is still reserved
- **WHEN** a policy declares a coverage contract with `scope: dependency_edge`
- **THEN** validation fails with an error explaining that `dependency_edge` coverage is not yet implemented

#### Scenario: A project or assembly coverage contract is accepted
- **WHEN** a policy declares a coverage contract with `scope: project` or `scope: assembly`
- **THEN** validation does not reject it for having an unsupported scope
