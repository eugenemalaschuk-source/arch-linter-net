## Why

Architecture rules often fail open when a new namespace or module is created outside the known layer list. Container layer templates make repeated structures easier to describe, but they also need an exhaustive mode so accidental new siblings are detected instead of ignored. This prevents silent architecture gaps when a developer adds a new feature namespace under a container root without updating the template.

## What Changes

- Add `exhaustive` boolean option to layer template contracts (`strict_layer_templates` / `audit_layer_templates`)
- When `exhaustive: true`, the linter scans loaded assemblies for child namespaces under each container that are not mapped into any declared layer
- Unmapped sibling namespaces that contain types produce a violation
- Unmapped sibling namespaces without types are silent (no false positives for empty placeholder namespaces)
- Strict exhaustive violations fail validation; audit exhaustive violations are reported without failing
- Diagnostics identify the container and the unmapped namespace
- JSON schema, OpenSpec layer-templates spec, and documentation are updated

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `layer-templates`: Add exhaustive container coverage requirement — template contracts can require all child namespaces under a container to be mapped into declared layers

## Impact

- **Model**: `ArchitectureLayerTemplateContract` gains `Exhaustive` YAML property; `ArchitectureLayerContract` gains `Exhaustive` runtime flag
- **Expander**: `LayerTemplateExpander` propagates `Exhaustive` from template to expanded contract
- **Runner**: `ArchitectureContractRunner.CheckLayerContract` adds exhaustive sibling namespace detection
- **Schema**: `schema/dependencies.arch.schema.json` gains `exhaustive` on `layerTemplateContract`
- **Tests**: New test cases for exhaustive success, failure, silent empty, and backward compatibility
- **Docs**: `docs/contracts/index.md` updated with exhaustive example
