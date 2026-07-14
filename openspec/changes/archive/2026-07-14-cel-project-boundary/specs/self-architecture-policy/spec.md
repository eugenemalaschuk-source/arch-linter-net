## MODIFIED Requirements

### Requirement: Unity asmdef validation is a Core capability
The repository SHALL keep `.asmdef` validation in `ArchLinterNet.Core.Asmdef` and SHALL NOT maintain a separate `ArchLinterNet.Unity` production or test assembly solely for the asmdef convenience facade.

#### Scenario: Repository package and assembly inventory is evaluated
- **WHEN** the solution, self-policy, release workflow, and package documentation are inspected
- **THEN** they contain `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and `ArchLinterNet.CEL` production packages
- **AND** asmdef facade tests run from `ArchLinterNet.Core.Tests`
