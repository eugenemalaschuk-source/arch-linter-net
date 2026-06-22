# Contract ID Selection Specification

## Purpose
Lets contracts declare a stable optional ID and lets the CLI select a subset of contracts to run by ID.

## Requirements

### Requirement: Contract model accepts optional stable ID
All 7 contract types SHALL accept an optional `id` field in YAML. When `id` is omitted, the system SHALL derive one from `name` by lowercasing and replacing space runs with hyphens.

#### Scenario: Explicit ID in YAML
- **WHEN** a contract YAML entry includes `id: map-core-boundary`
- **THEN** the deserialized contract has `Id == "map-core-boundary"`

#### Scenario: Omitted ID falls back to name
- **WHEN** a contract YAML entry has `name: Map.Core must not depend on Runtime` and no `id`
- **THEN** the deserialized contract has `Id == "map-core-must-not-depend-on-runtime"`

#### Scenario: Name with special characters
- **WHEN** a contract has `name: Assembly A -> Assembly B` and no `id`
- **THEN** the fallback ID SHALL be `"assembly-a-to-assembly-b"` (arrow replaced)

### Requirement: Duplicate IDs fail policy loading
The system SHALL validate that no two contracts of the same type within the same mode group (strict or audit) share the same ID. Duplicate IDs SHALL raise an `InvalidOperationException` with a diagnostic message listing the duplicate.

#### Scenario: Duplicate IDs in same group
- **WHEN** two strict dependency contracts both have `id: my-rule`
- **THEN** loading fails with `InvalidOperationException` mentioning `"my-rule"`

#### Scenario: Same ID across different contract types allowed
- **WHEN** a strict dependency contract has `id: boundary` and a strict layer contract also has `id: boundary`
- **THEN** loading succeeds (different types, no conflict)

#### Scenario: Same ID across strict and audit allowed
- **WHEN** a strict contract has `id: my-rule` and an audit contract also has `id: my-rule`
- **THEN** loading succeeds (different groups, no conflict)

### Requirement: CLI supports --contract for selective execution
The CLI SHALL accept `--contract <id>` (multi-value). When specified, only contracts with matching IDs SHALL be executed. Strict and audit modes both SHALL support filtering.

#### Scenario: Single contract selection
- **WHEN** the CLI is invoked with `--contract map-core-boundary`
- **THEN** only the contract with ID `map-core-boundary` is executed

#### Scenario: Multiple contract selection
- **WHEN** the CLI is invoked with `--contract foo --contract bar`
- **THEN** both contracts with IDs `foo` and `bar` are executed

#### Scenario: Unknown contract ID
- **WHEN** the CLI is invoked with `--contract nonexistent`
- **THEN** exit code 2 is returned with a diagnostic listing unknown and valid IDs

#### Scenario: Filtering works in audit mode
- **WHEN** the CLI is invoked with `--mode audit --contract audit-rule`
- **THEN** only the audit contract with ID `audit-rule` is executed

#### Scenario: Configuration checks run regardless of filter
- **WHEN** the CLI is invoked with `--contract some-id` and there are missing assemblies
- **THEN** missing assembly warnings are still reported (configuration checks not filtered)

### Requirement: Output includes contract ID
Human-readable and JSON output SHALL include the contract ID alongside the contract name.

#### Scenario: Human output includes ID
- **WHEN** a contract with `id: my-rule` produces violations
- **THEN** the formatted output includes `[my-rule]` in the violation line

#### Scenario: JSON output includes contract_id
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the JSON output contains `"contract_id": "my-rule"` in the violation object

#### Scenario: Contract without explicit ID still shows fallback ID
- **WHEN** a contract has no explicit `id` but has `name: My Rule`
- **THEN** the output includes `"contract_id": "my-rule"` (the normalized fallback)
