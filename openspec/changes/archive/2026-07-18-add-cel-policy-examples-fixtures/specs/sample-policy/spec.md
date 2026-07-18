## ADDED Requirements

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
with diagnostic evidence, a strict violation failing the run while the same
rule declared under the audit group does not fail the run, and at least one
violation produced by a contract using an explicit CEL `when` predicate.

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

#### Scenario: Strict violation fails the run while the audit equivalent does not
- **WHEN** the same underlying rule shape is declared once under a `strict_*`
  group and once under the matching `audit_*` group in separate fixture runs
- **THEN** the strict run SHALL exit with a non-zero exit code and the audit
  run SHALL report the finding without the strict run's contract being
  affected

#### Scenario: JSON output includes diagnostic evidence for a fixture violation
- **WHEN** `ValidateCommandHandler` runs with `--format json` against a
  violating fixture for the port-boundary, anti-corruption, or layout
  contract shape
- **THEN** the JSON output SHALL include the violating type's identity and
  the contract's forbidden/expected evidence fields
