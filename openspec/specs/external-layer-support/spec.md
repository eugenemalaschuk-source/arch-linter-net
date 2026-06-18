## ADDED Requirements

### Requirement: Layer can be declared external

The system SHALL support an optional `external` boolean property on layer declarations in the policy YAML.

```yaml
layers:
  unity_engine:
    namespace: UnityEngine
    external: true
```

- The `external` field SHALL default to `false` when not specified
- The `external` field SHALL be optional — existing policies without it remain valid
- The `external` field SHALL be independent of `namespace` and `namespace_suffix`

#### Scenario: External flag deserializes from YAML

- **WHEN** a policy YAML contains `external: true` on a layer
- **THEN** the deserialized `ArchitectureLayer.External` property SHALL be `true`

#### Scenario: External flag defaults to false

- **WHEN** a policy YAML does not contain `external` on a layer
- **THEN** the deserialized `ArchitectureLayer.External` property SHALL be `false`

#### Scenario: External flag validates in JSON Schema

- **WHEN** a policy YAML is validated against the JSON Schema
- **THEN** `external: true` SHALL be valid for any layer definition

### Requirement: External layers suppress configuration empty-layer diagnostic

The system SHALL NOT produce an `"empty layer namespace"` configuration violation for any layer where `external: true`.

- The suppression SHALL apply regardless of `strict` or `audit` mode
- The suppression SHALL only affect the configuration pre-check — contract checks remain unchanged

#### Scenario: External empty layer produces no configuration violation

- **WHEN** `CheckConfiguration()` is called with a policy containing a layer where `external: true` and the namespace has no types in loaded assemblies
- **THEN** no `"empty layer namespace"` violation SHALL be produced for that layer

#### Scenario: Non-external empty layer still produces violation

- **WHEN** `CheckConfiguration()` is called with a layer where `external` is `false` (or absent) and the namespace has no types in loaded assemblies
- **THEN** an `"empty layer namespace"` violation SHALL be produced for that layer

#### Scenario: External layer with types found is used normally

- **WHEN** `CheckConfiguration()` is called with a layer where `external: true` and types ARE found in loaded assemblies
- **THEN** no `"empty layer namespace"` violation SHALL be produced for that layer

### Requirement: External layers work in all contract checks

External layers SHALL be fully functional as targets in all contract types: dependency contracts, layer contracts, allow-only contracts, cycle contracts, independence contracts, and method-body contracts.

- The dependency scanning logic SHALL NOT distinguish between external and non-external layers
- Namespace string matching SHALL work identically for both

#### Scenario: External layer as forbidden target in dependency contract

- **WHEN** a dependency contract has a forbidden target layer with `external: true`
- **THEN** source types referencing the external layer's namespace SHALL produce violations

#### Scenario: External layer in layer contract

- **WHEN** a layer contract includes an external layer in its layer list
- **THEN** the layering constraint SHALL be enforced using the external layer's namespace

#### Scenario: External layer in allow-only contract

- **WHEN** an allow-only contract includes an external layer in its allowed list
- **THEN** references to the external layer's namespace in source types SHALL be permitted

#### Scenario: External layer in independence contract

- **WHEN** an independence contract includes an external layer
- **THEN** source types in other layers referencing the external layer's namespace SHALL produce violations

#### Scenario: External layer as source produces no contract violations

- **WHEN** an external layer with no loaded types is used as a `source` in any contract
- **THEN** the contract SHALL produce no violations from that source (no types to scan)
