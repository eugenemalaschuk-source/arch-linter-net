## MODIFIED Requirements

### Requirement: Asmdef validation belongs to Core
The repository SHALL keep asmdef validation in `ArchLinterNet.Core.Asmdef` and SHALL model the production package graph as Core, CLI, and Testing. The asmdef convenience facade is part of Core rather than its own production or test assembly.

#### Scenario: Repository assembly inventory is evaluated
- **WHEN** the solution, self-policy, release workflow, and package documentation are inspected
- **THEN** they contain Core, CLI, and Testing production packages
- **AND** asmdef facade tests run from `ArchLinterNet.Core.Tests`
