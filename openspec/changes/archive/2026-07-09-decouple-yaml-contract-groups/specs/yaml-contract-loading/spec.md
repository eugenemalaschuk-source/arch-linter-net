## MODIFIED Requirements

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

## ADDED Requirements

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
