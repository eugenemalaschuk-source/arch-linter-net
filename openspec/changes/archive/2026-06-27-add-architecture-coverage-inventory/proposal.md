## Why

Future coverage contract families (#98-#103) all need the same first-party facts — namespaces, representative types, declared layers, expanded layer templates, project/assembly data, and dependency edges — to classify coverage. Without a shared inventory, each contract family would duplicate scanning logic already implemented by `ArchitectureTypeIndex`, `ArchitectureLayerResolver`, `LayerTemplateExpander`, `ArchitectureProjectDiscovery`, and `ArchitectureReferenceGraph`. Issue #97 builds that shared, deterministic inventory now so later coverage work can consume it instead of re-deriving it.

## What Changes

- Add `ArchitectureCoverageInventory`, built once per analysis session from existing discovery/resolution/execution components, exposing:
  - First-party namespaces (deterministically sorted, deduplicated).
  - A representative type per namespace for diagnostic evidence.
  - Declared layers (reusing `ArchitectureLayerResolver`/`NamespaceGlobPattern`).
  - Expanded layer templates via `LayerTemplateExpander.Expand()`, preserving the `Exhaustive` flag.
  - Project/assembly facts from `ArchitectureProjectDiscovery`'s `ProjectDiscoveryResult` when available.
  - Dependency edges aggregated from `ArchitectureReferenceGraph` at namespace/layer granularity, sorted by source then target.
- No classification (covered/uncovered/stale/etc.), no exclusion handling, and no coverage contract family wiring — those remain reserved for the issues that implement actual coverage contracts. The existing `ArchitectureRunnerFactory` guard that rejects declared `strict_coverage`/`audit_coverage` contracts is unchanged.
- Add unit tests verifying deterministic construction from realistic fixtures.

## Capabilities

### New Capabilities
- `architecture-coverage-inventory`: Deterministic inventory engine collecting first-party namespaces, representative types, layers, expanded templates, project/assembly facts, and dependency edges for future coverage contracts to consume.

### Modified Capabilities
(none — `architecture-coverage-model` remains the design-only vocabulary spec; this change does not alter its requirements)

## Impact

- New file(s) under `src/ArchLinterNet.Core/Execution/` (e.g. `ArchitectureCoverageInventory.cs`).
- No change to `ArchitectureContractRunner` wiring, `ArchitectureRunnerFactory` coverage-reservation guard, or YAML contract schema.
- New unit tests under `tests/ArchLinterNet.Core.Tests/`.
- No CLI-facing or reporting changes.
