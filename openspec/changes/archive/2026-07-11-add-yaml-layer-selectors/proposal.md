## Why

Semantic classification now produces a per-run role index, but YAML layers still
only match namespaces. This leaves the reviewed `layers.<name>.selector` shape
inert and prevents existing layer-based contracts from expressing boundaries by
role and metadata.

## What Changes

- Bind `layers.<name>.selector` to the policy model and match role plus exact,
  AND-combined metadata against the per-run role index.
- Allow selector-only layers and define deterministic AND behavior when
  `namespace` and `selector` are both present.
- Preserve namespace-only, glob, suffix, and external-layer behavior.
- Make selector-backed layers usable through existing layer-based contract
  paths where a type set can be resolved, including dependency, layer,
  allow-only, cycle, independence, protected, and external-source paths.
- Emit deterministic configuration diagnostics for invalid or empty selectors
  and describe whether a layer match came from namespace or semantic selection.
- Update the JSON schema, semantic-layer documentation, examples, and NUnit
  regression/feature tests.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `semantic-classification-model`: make the reviewed layer selector shape
  executable, including selector-only layers, exact role/metadata matching,
  diagnostics, and compatibility rules.

## Impact

- Core policy models, layer resolution, type/role indexes, contract execution,
  coverage/configuration diagnostics, and the dependency-policy JSON schema.
- Existing namespace policies remain source-compatible; selector support adds
  no package or external dependency.
- Public documentation and sample policy shape change to show semantic layers.
