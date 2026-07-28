## ADDED Requirements

### Requirement: Schema discovery is a composed command module
The CLI SHALL expose offline schema registry discovery through a top-level command module and instance handler that receive the packaged-schema registry through the existing Core composition seam. The command SHALL not read repository-relative schema paths or perform network, restore, build, or target-assembly operations.

#### Scenario: Schema command is composed as a module
- **WHEN** the CLI root command is built
- **THEN** the schema discovery command is registered by a command module without a hard-coded top-level command list
