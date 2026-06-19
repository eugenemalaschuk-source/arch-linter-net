## ADDED Requirements

### Requirement: Protected violation human output includes layer context
The human-readable formatter SHALL include source layer, target layer, and allowed importers in protected contract violation output.

#### Scenario: Protected violation human format
- **WHEN** a protected contract violation occurs with `source_layer = "web"`, `target_layer = "map_core_internal"`, and `allowed_importers = ["map_core"]`
- **THEN** the human output SHALL contain the source type, the protected target type, the protected layer name, and the allowed importers list

### Requirement: Protected violation JSON includes structured fields
The JSON formatter SHALL emit `source_layer`, `target_layer`, and `allowed_importers` fields when the violation originates from a protected contract.

#### Scenario: Protected violation JSON enrichment
- **WHEN** a protected contract violation is serialized to JSON
- **THEN** the JSON object SHALL contain `"source_layer"`, `"target_layer"`, and `"allowed_importers"` alongside the standard fields

### Requirement: Backward-compatible JSON for non-protected violations
Non-protected contract violations SHALL NOT include `source_layer`, `target_layer`, or `allowed_importers` in JSON output.

#### Scenario: Existing violation unchanged
- **WHEN** a standard `strict` contract violation is serialized to JSON
- **THEN** the JSON SHALL NOT contain protected-specific fields
