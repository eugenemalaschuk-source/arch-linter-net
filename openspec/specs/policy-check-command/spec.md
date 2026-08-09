# policy-check-command Specification

## Purpose
TBD - created by archiving change policy-only-validation. Update Purpose after archive.
## Requirements
### Requirement: Assembly-free policy check command
The system SHALL provide `arch-linter-net policy check --policy <path>` to validate
policy syntax, imports, composition, contract IDs, and static configuration without
building projects or loading target assemblies.

#### Scenario: Clean checkout policy is valid
- **WHEN** a valid decomposed policy is checked with no compiled target assemblies
  present
- **THEN** the command completes successfully and reports the completed
  policy/configuration checks

#### Scenario: Discovery-wide coverage contract is valid
- **WHEN** a valid direct policy declares project- or assembly-scope coverage without
  `roots`
- **THEN** policy check accepts the policy without building projects or loading target
  assemblies

#### Scenario: Policy requires repository facts
- **WHEN** a selector or contract requires assemblies, project evaluation, or source
  facts
- **THEN** the command reports it as typed deferred state and does not report
  architecture compliance

### Requirement: Deterministic policy-check boundary
The policy-check operation SHALL not invoke MSBuild, evaluate projects, load target assemblies, or depend on existing `bin` or `obj` state.

#### Scenario: Instrumented command execution
- **WHEN** policy check is executed under build and assembly-load instrumentation
- **THEN** neither build invocation nor target-assembly loading is observed

### Requirement: Policy check result status
The policy-check operation SHALL distinguish completed checks, deferred checks, and typed failures in its result. A valid policy with deferred checks SHALL exit `0`; malformed or invalid policy/configuration input SHALL exit `2`.

#### Scenario: Invalid imported fragment
- **WHEN** an imported fragment has malformed schema or static configuration
- **THEN** the command exits `2` and exposes the typed failure without presenting a successful policy result

### Requirement: Policy-check JSON errors are authoritative

When `policy check --format json` terminates on an owned command, policy, or configuration failure, it SHALL retain its existing single structured JSON document on stdout with policy diagnostic details where available. Its exit code and human-format stderr output SHALL remain unchanged.

#### Scenario: Invalid policy is parseable JSON
- **WHEN** `policy check --format json` loads an invalid policy
- **THEN** stdout parses as one JSON error document containing the structured policy failure

