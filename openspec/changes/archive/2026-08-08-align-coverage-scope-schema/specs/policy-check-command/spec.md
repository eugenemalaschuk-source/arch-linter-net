## MODIFIED Requirements

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
