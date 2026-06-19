## ADDED Requirements

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
