# YAML Contract Loading Specification

## Purpose
Loads an architecture contract document from a YAML file path or repository root.
## Requirements
### Requirement: Load architecture contract from YAML file path
The system SHALL load and deserialize an `architecture/dependencies.arch.yml` file from a given file path into an `ArchitectureContractDocument` instance, via `IArchitecturePolicyDocumentLoader` (an instance service registered in `AddArchLinterNetCore()`) rather than a static `ArchitectureContractLoader` class.

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
- **THEN** `IArchitecturePolicyDocumentLoader.LoadFromRepositoryRoot(repositoryRoot)` returns the deserialized document

### Requirement: Auto-discover repository root
The system SHALL auto-discover the repository root by walking parent directories from `AppContext.BaseDirectory` until `architecture/dependencies.arch.yml` is found, via `IArchitectureRepositoryRootResolver` (an instance service registered in `AddArchLinterNetCore()`) rather than a static `ArchitectureRepositoryRootLocator` class, and SHALL NOT cache the resolved root in a process-wide static field.

#### Scenario: Contract file found in ancestor directory
- **WHEN** `architecture/dependencies.arch.yml` exists in a parent of `AppContext.BaseDirectory`
- **THEN** `IArchitectureRepositoryRootResolver.Resolve()` returns that ancestor directory

#### Scenario: Contract file not found
- **WHEN** no ancestor directory contains `architecture/dependencies.arch.yml`
- **THEN** the system throws `DirectoryNotFoundException`

### Requirement: YAML schema supports all contract types
The deserialized document SHALL contain one strict/audit contract-list pair per registered contract family, as defined by the `Contracts`-local contract family binding registry, rather than a fixed, hand-enumerated count. Adding a new contract family SHALL NOT require editing a single central mega DTO file; the family's YAML-bound properties and contract POCO SHALL live in their own file under `src/ArchLinterNet.Core/Contracts/Families/`, contributed to `ArchitectureContractGroups` via a C# `partial class`, and registered once in the binding registry.

#### Scenario: Complete YAML with all contract types
- **WHEN** a YAML file defines every contract group known to the binding registry
- **THEN** the `ArchitectureContractGroups` property contains a populated list for each registered family

#### Scenario: Minimal YAML with empty contract groups
- **WHEN** a YAML file defines only `version`, `name`, `layers`, `analysis`, and empty contract lists
- **THEN** the deserialized document has empty lists for all contract groups

#### Scenario: Adding a new contract family does not require editing the shared model file
- **WHEN** a future task adds a new contract family
- **THEN** it does so by adding a new file under `src/ArchLinterNet.Core/Contracts/Families/` plus one new entry in the binding registry, without editing the existing per-family files or the `EnumerateStrict`/`EnumerateAudit`-equivalent enumeration logic

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

### Requirement: Contract family enumeration is registry-driven, not hand-enumerated
The system SHALL expose a `Contracts`-local contract family binding registry that is the single source of truth, within `Contracts`, for which contract families exist, their strict/audit accessors, and whether they participate in the document's aggregate `AllStrict`/`AllAudit` contract enumeration. `ArchitectureContractGroups.AllStrict`/`AllAudit` and `DuplicateIdValidator` SHALL both derive their family lists from this registry rather than independent hand-written enumerations, each preserving its own current family subset.

#### Scenario: Aggregate enumeration excludes template-only families
- **WHEN** `ArchitectureContractGroups.AllStrict` or `AllAudit` is enumerated
- **THEN** it includes every registered family flagged for aggregate enumeration and excludes `layer_template`, matching current behavior

#### Scenario: Duplicate-ID validation covers every family including template-only ones
- **WHEN** `DuplicateIdValidator` validates a document
- **THEN** it checks every registered family's strict and audit groups for duplicate IDs, including `layer_template`, matching current behavior

#### Scenario: Registry stays local to Contracts
- **WHEN** the contract family binding registry is defined
- **THEN** it does not reference or depend on any type in `ArchLinterNet.Core.Execution`, preserving the constraint that `Contracts` depends on nothing else in `Core`

### Requirement: Public schema supports semantic coverage contracts
The published JSON Schema SHALL accept `scope: semantic_role`, semantic exclusion `role` and `metadata` fields, and the documented coverage contract-level fields.

#### Scenario: Schema-aware authoring accepts semantic coverage
- **WHEN** a policy author validates a semantic-role coverage contract against the published JSON Schema
- **THEN** the contract and its reasoned semantic exclusion are accepted

### Requirement: Semantic coverage exclusions reject unknown keys
Before permissive YAML deserialization, the loader SHALL reject an unknown field in a semantic-role coverage exclusion mapping.

#### Scenario: Misspelled semantic exclusion metadata key
- **WHEN** a semantic-role coverage exclusion contains `metdata` instead of `metadata`
- **THEN** policy loading fails with a diagnostic naming the unknown key
- **AND** the exclusion is not interpreted as a role-wide exclusion

### Requirement: Semantic exclusion metadata must be an object
The loader SHALL reject `metadata: null` in a semantic-role coverage exclusion before execution.

#### Scenario: Null exclusion metadata is rejected
- **WHEN** a semantic-role coverage exclusion declares `metadata: null`
- **THEN** loading fails with a null-metadata diagnostic

