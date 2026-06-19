## Why

ArchLinterNet today can forbid source layers from depending on forbidden targets, but cannot express the inverse: "this layer is an internal/protected surface that only approved consumers may reference." Without this, governing internal namespaces like `*.Internal`, `*.Infrastructure`, or `*.Generated` requires every consuming layer to opt-in via a forbidden contract — brittle when new layers join.

## What Changes

- Add `strict_protected` and `audit_protected` contract groups to the YAML model
- New `ArchitectureProtectedContract` model class with `protected` (target layers) and `allowed_importers` (approved consumer layers)
- Protected contract enforcement: any reference to a protected layer from a non-allowed layer is a violation
- Self-references (within the protected layer) are implicitly allowed
- Support `allowed_types` for type-level overrides and `ignored_violations` for baselining
- JSON Schema updated with `protectedContract` definition
- Human and JSON diagnostic output updated for protected violation context
- Self-architecture contract updated to protect ArchLinterNet.Core internals
- Documentation and AI capability manifests updated

## Capabilities

### New Capabilities
- `protected-surface-contracts`: Define layers as protected surfaces with an explicit allow-list of importer layers. Strict and audit modes. Type-level exceptions via `allowed_types` and violation baselining via `ignored_violations`.

### Modified Capabilities
- `violation-reporting`: Structured JSON output enriched with `source_layer`, `target_layer`, `allowed_importers` for protected contract violations

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — new model class
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.cs` — new `CheckProtectedContract` method + accessors
- `src/ArchLinterNet.Core/ArchitectureValidator.cs` — new loop
- `src/ArchLinterNet.Testing/ArchitectureAssertions.cs` — new loop
- `src/ArchLinterNet.Cli/Program.cs` — new loop + contract ID collection
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractLoader.cs` — duplicate ID validation group
- `schema/dependencies.arch.schema.json` — new `protectedContract` definition
- `architecture/dependencies.arch.yml` — protect Core internals
- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs` — optional init-only fields
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs` — enriched output
