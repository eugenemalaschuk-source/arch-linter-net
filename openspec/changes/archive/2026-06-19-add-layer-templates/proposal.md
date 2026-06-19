## Why

Many repositories repeat the same internal architecture shape across multiple modules or feature roots. Without container-based layer templates, policy authors must duplicate many similar contracts, which is noisy for humans and error-prone for AI agents. Adding reusable layer templates that can be applied to multiple namespace containers addresses this gap, inspired by Python Import Linter's `layers` contracts with `containers` and optional layers.

## What Changes

- New `strict_layer_templates` / `audit_layer_templates` contract groups in the YAML schema
- A template applies the same ordered layer contract to multiple namespace containers
- Relative layer names are resolved under each container by prefixing the container namespace
- Optional layers (marked `optional: true`) do not fail validation when absent
- Existing `strict_layers` / `audit_layers` contracts continue to work unchanged
- Diagnostics identify the concrete container that violated the template
- JSON Schema (`dependencies.arch.schema.json`) updated with new definitions
- YAML reference docs and contracts docs updated
- AI policy-authoring manifest updated

## Capabilities

### New Capabilities

- `layer-templates`: Container-based layer template contracts with optional layer support in the architecture policy YAML format. Covers YAML structure, deserialization, template expansion, validation, and diagnostics.

### Modified Capabilities

<!-- No existing spec requirements are changing; layer templates are additive and backward compatible. -->

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — new DTOs for template contracts
- `src/ArchLinterNet.Core/` — new `LayerTemplateExpander` component; modified `ArchitectureValidator`, `ArchitectureContractRunner`
- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs` — optional template/container metadata
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs` — template-aware formatting
- `schema/dependencies.arch.schema.json` — new contract group definitions
- `docs/reference/yaml-schema.md` and `docs/contracts/index.md` — updated documentation
- `tests/ArchLinterNet.Core.Tests/` — new test files for expander and template layer contracts
