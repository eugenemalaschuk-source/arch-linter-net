# YAML Contract Loading Specification

## Purpose
Loads an architecture contract document from a YAML file path or repository root.

## Requirements

### Requirement: Load architecture contract from YAML file path
The system SHALL load and deserialize an `architecture/dependencies.arch.yml` file from a given file path into an `ArchitectureContractDocument` instance.

#### Scenario: Valid YAML file
- **WHEN** a valid `dependencies.arch.yml` file exists at the specified path
- **THEN** the system returns a fully populated `ArchitectureContractDocument` with all top-level blocks (`version`, `name`, `layers`, `legacy_runtime_layers`, `analysis`, `contracts`)

#### Scenario: Missing YAML file
- **WHEN** no file exists at the specified path
- **THEN** the system throws `FileNotFoundException` with the path in the message

#### Scenario: Invalid YAML content
- **WHEN** the file contains malformed YAML that cannot be deserialized
- **THEN** the system throws `InvalidOperationException` indicating deserialization failure

### Requirement: Load architecture contract from repository root
The system SHALL resolve `architecture/dependencies.arch.yml` relative to a repository root directory path.

#### Scenario: Repository root with contract file
- **WHEN** a directory contains `architecture/dependencies.arch.yml`
- **THEN** `LoadFromRepositoryRoot(repositoryRoot)` returns the deserialized document

### Requirement: Auto-discover repository root
The system SHALL auto-discover the repository root by walking parent directories from `AppContext.BaseDirectory` until `architecture/dependencies.arch.yml` is found.

#### Scenario: Contract file found in ancestor directory
- **WHEN** `architecture/dependencies.arch.yml` exists in a parent of `AppContext.BaseDirectory`
- **THEN** `ArchitectureRepositoryRootLocator.Resolve()` returns that ancestor directory

#### Scenario: Contract file not found
- **WHEN** no ancestor directory contains `architecture/dependencies.arch.yml`
- **THEN** the system throws `DirectoryNotFoundException`

### Requirement: YAML schema supports all contract types
The deserialized document SHALL contain all 14 contract groups: `strict`, `audit`, `strict_layers`, `audit_layers`, `strict_allow_only`, `audit_allow_only`, `strict_cycles`, `audit_cycles`, `strict_method_body`, `audit_method_body`, `strict_asmdef`, `audit_asmdef`, `strict_independence`, `audit_independence`.

#### Scenario: Complete YAML with all contract types
- **WHEN** a YAML file defines all 14 contract groups
- **THEN** the `ArchitectureContractGroups` property contains all 14 populated lists

#### Scenario: Minimal YAML with empty contract groups
- **WHEN** a YAML file defines only `version`, `name`, `layers`, `analysis`, and empty contract lists
- **THEN** the deserialized document has empty lists for all contract groups


### Requirement: YAML deserialization accepts optional id field
The deserializer SHALL accept an optional `id` field on all contract types without throwing for unmatched properties.

#### Scenario: Valid YAML with id field
- **WHEN** a contract entry defines `id: my-rule`
- **THEN** the deserialized contract has `Id == "my-rule"`

#### Scenario: Omitted id field
- **WHEN** a contract entry omits `id`
- **THEN** the deserialized contract has `Id == null` (fallback applied post-deserialization)

### Requirement: Post-deserialization ID normalization and validation
After deserialization, the system SHALL compute fallback IDs for contracts without an explicit `id` and validate that no duplicate IDs exist within the same contract type and mode group.

#### Scenario: Fallback ID computed
- **WHEN** a contract has no explicit `id`
- **THEN** the loader populates `Id` from `name` via normalization

#### Scenario: Duplicate ID detected
- **WHEN** two contracts in the same group and type share an ID
- **THEN** an `InvalidOperationException` is thrown with a diagnostic message
