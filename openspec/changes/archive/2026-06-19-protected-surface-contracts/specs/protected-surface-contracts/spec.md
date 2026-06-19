## ADDED Requirements

### Requirement: Evaluate protected contracts
The system SHALL verify that types outside allowed importer layers do not reference types in protected layers.

#### Scenario: Non-allowed layer references protected type
- **WHEN** a type in `web` layer references `Map.Core.Internal.Service` and layer `map_core_internal` is protected with `allowed_importers: [map_core]`
- **THEN** a violation SHALL be reported with `source_layer = "web"` and `target_layer = "map_core_internal"`

#### Scenario: Allowed importer references protected type
- **WHEN** a type in `map_core` layer references `Map.Core.Internal.Service` and `map_core` is in `allowed_importers`
- **THEN** no violation SHALL be reported for that reference

#### Scenario: Self-reference within protected layer
- **WHEN** a type in `map_core_internal` layer references another type in `map_core_internal`
- **THEN** no violation SHALL be reported (intra-layer references are implicitly allowed)

#### Scenario: Multiple protected layers
- **WHEN** contracts define two protected layers with different `allowed_importers`
- **THEN** each contract SHALL be evaluated independently

### Requirement: Protected contract accepts `allowed_types`
The system SHALL allow `allowed_types` to exempt specific source types from protected contract violations.

#### Scenario: Allowed type overrides layer restriction
- **WHEN** a contract has `allowed_types: [Tools.MapDebugExporter]` and `Tools.MapDebugExporter` in `web` layer references a protected type
- **THEN** no violation SHALL be reported

#### Scenario: Allowed type does not exempt other types in same layer
- **WHEN** `allowed_types` lists a specific source type and a different type in the same layer references a protected type
- **THEN** a violation SHALL be reported for the non-exempt type

### Requirement: Protected contract accepts `ignored_violations`
The system SHALL support `ignored_violations` for baselining existing references to protected layers.

#### Scenario: Ignored violation suppressed
- **WHEN** a reference matches an `ignored_violations` entry
- **THEN** no violation SHALL be reported for that reference

### Requirement: Protected contract accepts optional `id`
A protected contract SHALL accept an optional `id` field. When provided, violations SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a protected contract with `id: core-internals` produces a violation
- **THEN** the violation SHALL have `ContractId == "core-internals"`

### Requirement: Protected violation includes source/layer context
Protected contract violations SHALL include `SourceLayer` (the layer of the violating type) and `TargetLayer` (the protected layer) and `AllowedImporters` when serialized to JSON.

#### Scenario: Protected violation JSON structure
- **WHEN** a protected contract violation occurs
- **THEN** the JSON output SHALL contain `source_layer`, `target_layer`, and `allowed_importers` fields if the violation originates from a protected contract

### Requirement: Protected contract validates layer references
Unrecognized layer names in `protected` or `allowed_importers` SHALL produce the same `InvalidOperationException` as other contract types.

#### Scenario: Unknown protected layer
- **WHEN** a protected contract references a layer key not in `layers`
- **THEN** the system SHALL throw `InvalidOperationException`
