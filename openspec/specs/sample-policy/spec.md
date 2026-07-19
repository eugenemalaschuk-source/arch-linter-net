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

### Requirement: Modular-monolith example demonstrates semantic port and layout contracts
The modular-monolith import example SHALL include a bounded context that
reaches another bounded context only through an approved `Port` seam, a
bounded context where a direct forbidden reference to another context's
`DomainLayer` or `Adapter` is declared, a legacy bounded context that reaches
infrastructure only through an approved `AntiCorruptionLayer` seam, and a
layout convention contract expressing a Services/Interfaces counterpart
pairing. Each contract SHALL use role and metadata names already defined in
`semantic-role-catalog`.

#### Scenario: Modular-monolith example loads with the new contracts
- **WHEN** the modular-monolith sample root and its fragments are loaded
  through `ArchitecturePolicyDocumentLoader`
- **THEN** the effective policy SHALL include a `strict_port_boundaries` entry
  with a non-empty `allowed_seams` and `forbidden` list, a
  `strict_port_boundaries` entry using an `AntiCorruptionLayer` allowed seam,
  and a `strict_layout_conventions` entry declaring
  `require_matching_interface`

### Requirement: Unity-client example demonstrates a layout convention contract
The Unity-client import example SHALL include a `layout_conventions` contract
expressing the sample's existing Runtime/Editor/Features fragment
classification as a static layout expectation (for example, forbidding a type
kind or name pattern associated with editor-only code from appearing in a
runtime-folder-selected file).

#### Scenario: Unity-client example loads with a layout convention contract
- **WHEN** the Unity-client sample root and its fragments are loaded through
  `ArchitecturePolicyDocumentLoader`
- **THEN** the effective policy SHALL include at least one
  `strict_layout_conventions` or `audit_layout_conventions` entry

### Requirement: Port-boundary, anti-corruption, and layout contract shapes are proven end-to-end
The repository SHALL contain a CLI-level fixture (compiled marker-attributed
types plus a policy YAML written and validated through the real
`ValidateCommandHandler`) that proves, for the port-boundary, anti-corruption,
and layout-convention contract shapes documented in
`docs/contracts/port-boundary.md` and `docs/contracts/layout-conventions.md`:
a passing case producing no violation, a violating case producing a violation
with diagnostic evidence, a strict violation failing a strict-mode run while
the same rule declared only under the audit group does not affect a
strict-mode run of that policy, and at least one violation produced by a
contract using an explicit CEL `when` predicate.

#### Scenario: Approved port seam passes and a forbidden direct reference fails
- **WHEN** a fixture type reaches another bounded context only through a
  selector-matched `Port` type
- **THEN** `ValidateCommandHandler` running in strict mode SHALL report no
  violation for that fixture type, and a second fixture type that directly
  references the other context's `DomainLayer` type SHALL be reported as a
  violation identifying the forbidden reference

#### Scenario: Anti-corruption seam passes and a direct infrastructure reference fails
- **WHEN** a fixture type reaches infrastructure only through a
  selector-matched `AntiCorruptionLayer` type
- **THEN** `ValidateCommandHandler` running in strict mode SHALL report no
  violation for that fixture type, and a second fixture type that directly
  references a database/infrastructure adapter type SHALL be reported as a
  violation

#### Scenario: Missing interface counterpart is a layout violation
- **WHEN** a fixture concrete service class has no corresponding `I`-prefixed
  interface fact
- **THEN** `ValidateCommandHandler` running in strict mode SHALL report a
  `require_matching_interface` violation identifying the class and the
  expected counterpart name

#### Scenario: A CEL when-refined layout rule narrows which fixture types are checked
- **WHEN** a `strict_layout_conventions` contract declares
  `files_matching.when` selecting a subset of a matched file's declared types
- **THEN** only fixture types for which the predicate evaluates `true` SHALL
  be checked against the contract's expectations, and the run SHALL report a
  violation for at least one such type

#### Scenario: An audit-only rule does not affect a strict-mode run
- **WHEN** a rule shape is declared only under an `audit_*` group, and the same
  policy is run once in `--mode audit` and once in `--mode strict`
- **THEN** the audit run SHALL report the finding (still exiting non-zero, per
  the documented exit-code convention where CI opts an audit run out via
  `continue-on-error` rather than the process itself succeeding) and the
  strict run SHALL exit 0 and SHALL NOT report that finding, because the rule
  is not declared under any `strict_*` group

#### Scenario: JSON output includes diagnostic evidence for a fixture violation
- **WHEN** `ValidateCommandHandler` runs with `--format json` against a
  violating fixture for the port-boundary, anti-corruption, or layout
  contract shape
- **THEN** the JSON output SHALL include the violating type's identity and
  the contract's forbidden/expected evidence fields

