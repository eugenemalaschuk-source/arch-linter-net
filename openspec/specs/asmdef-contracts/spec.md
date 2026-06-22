# Asmdef Contracts Specification

## Purpose
Validates Unity assembly definition (.asmdef) files against editor-only reference and forbidden-prefix restrictions.

## Requirements

### Requirement: Validate asmdef editor-only reference restrictions
The system SHALL detect when a source assembly references an editor-only assembly (where `includePlatforms == ["Editor"]`) and `forbidden_editor_refs` is true.

#### Scenario: Editor-only reference violation
- **WHEN** source assembly references an editor-only assembly and `forbidden_editor_refs` is true
- **THEN** a violation is returned listing the forbidden editor reference

#### Scenario: No editor-only references
- **WHEN** source assembly does not reference any editor-only assemblies
- **THEN** no editor-related violations are reported

### Requirement: Validate asmdef prefix-based restrictions
The system SHALL detect when a source assembly references an assembly whose name starts with a forbidden prefix.

#### Scenario: Prefix match violation
- **WHEN** source assembly references `Some.Forbidden.Assembly` and `"Some.Forbidden.*"` is in `forbidden_asmdef_prefixes`
- **THEN** a violation is returned listing the forbidden reference

#### Scenario: Self-reference excluded
- **WHEN** the referenced assembly name equals the source assembly name
- **THEN** that reference is not reported as a violation

### Requirement: Parse Unity .asmdef JSON files
The system SHALL recursively scan a configurable root directory for `*.asmdef` files and parse their `name`, `references`, and `includePlatforms` fields.

#### Scenario: Valid .asmdef files found
- **WHEN** the asmdef root directory contains valid `.asmdef` files
- **THEN** the scanner builds an internal map of assembly name to references and platforms

#### Scenario: Malformed .asmdef file
- **WHEN** a `.asmdef` file contains invalid JSON
- **THEN** that file is skipped without error


### Requirement: Asmdef contract accepts optional id
An asmdef contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an asmdef contract with `id: unity-rules` produces a violation
- **THEN** the violation SHALL have `ContractId == "unity-rules"`

#### Scenario: Violation without explicit ID
- **WHEN** an asmdef contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
