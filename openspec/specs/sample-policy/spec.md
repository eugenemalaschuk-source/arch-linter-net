# Sample Policy Specification

## Purpose
Provides a sample YAML policy and documents sample CLI usage for new adopters.
## Requirements
### Requirement: Sample YAML policy exists
The repository SHALL contain a sample YAML architecture contract file in `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` that demonstrates a realistic clean-architecture policy with strict contracts, layer contracts, cycle contracts, independence contracts, and audit-mode contracts.

#### Scenario: Sample policy is valid YAML
- **WHEN** the sample policy is loaded by `ArchitectureContractLoader.LoadFromPath`
- **THEN** no parse errors SHALL occur

#### Scenario: Sample policy has at least one strict contract
- **WHEN** the sample policy is inspected
- **THEN** it SHALL contain at least one contract under the `strict` key

#### Scenario: Sample policy has at least one audit contract
- **WHEN** the sample policy is inspected
- **THEN** it SHALL contain at least one contract under the `audit` key

### Requirement: Sample CLI usage is documented
The README SHALL document how to run the CLI against the sample policy:
```bash
dotnet run --project src/ArchLinterNet.Cli -- --policy samples/BasicCleanArchitecture/architecture/dependencies.arch.yml --mode strict
```

#### Scenario: CLI runs against sample policy
- **WHEN** the documented CLI command is run against the sample policy
- **THEN** exit code SHALL be 0 (the sample policy SHALL pass its own contracts)

### Requirement: Sample test adapter usage is documented
The README SHALL contain a code snippet showing how to use `ArchitectureAssertions` with NUnit, referencing the sample policy.

#### Scenario: README contains NUnit example
- **WHEN** the README is read
- **THEN** it SHALL contain a code block showing `ArchitectureAssertions.FromPolicy(...)` usage with NUnit

### Requirement: Modular-monolith import example is realistic and executable
The repository SHALL provide a modular-monolith example with one root policy that keeps appropriate shared settings inline and imports focused shared-layer and bounded-context fragments. The example SHALL use documented, executable fields and SHALL be loadable by the production policy loader.

#### Scenario: Modular-monolith example is loaded
- **WHEN** the sample root and its fragments are loaded through `ArchitecturePolicyDocumentLoader`
- **THEN** shared layers, bounded-context definitions, and ordered contracts compose into one valid effective policy

### Requirement: Unity client import example is realistic and executable
The repository SHALL provide a Unity/client example with one root policy that imports focused runtime, editor, and feature fragments while retaining a single execution entry point. The example SHALL use documented, executable fields and SHALL be loadable by the production policy loader.

#### Scenario: Unity client example is loaded
- **WHEN** the sample root and its fragments are loaded through `ArchitecturePolicyDocumentLoader`
- **THEN** runtime, editor, feature, external dependency, and asmdef concerns compose into one valid effective policy

