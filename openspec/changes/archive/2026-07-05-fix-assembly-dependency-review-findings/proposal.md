## Why

Code review on PR #184 flagged two consistency gaps in the `assembly_dependency`/`assembly_allow_only` `dependency_depth` hardening: the JSON schema still listed `transitive` as a valid enum value even though the loader rejects it (schema was softer than the runtime contract), and the direct-only guard only ran inside the YAML policy loader, so a contract built programmatically (bypassing the loader) with `dependency_depth: transitive` would silently be evaluated as direct instead of failing fast.

## What Changes

- JSON schema `dependency_depth` enum for `assemblyDependencyContract`/`assemblyAllowOnlyContract` narrows from `["direct", "transitive"]` to `["direct"]`, so schema-validating tools reject `transitive` the same way the loader does.
- `CheckAssemblyDependencyContract`/`CheckAssemblyAllowOnlyContract` now defensively re-check `DependencyDepth` and throw the same actionable error as the loader if it isn't `Direct`, closing the gap for contracts constructed directly against the session API rather than loaded from YAML.
- Docs updated to describe both enforcement points (schema + loader + defensive runtime guard).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `assembly-dependency-contracts`: defensive runtime rejection of `dependency_depth: transitive` for programmatically constructed contracts, in addition to the existing load-time rejection.
- `assembly-allow-only-contracts`: same defensive runtime rejection.

## Impact

- `schema/dependencies.arch.schema.json`: `dependency_depth` enum narrowed to `["direct"]` for both `$defs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyDependency.cs`: shared `RequireDirectDependencyDepth` guard called from both check methods.
- `docs/reference/yaml-schema.md`.
- Tests under `tests/ArchLinterNet.Core.Tests/AssemblyDependencyContractTests.cs` and `AssemblyAllowOnlyContractTests.cs` (programmatic transitive-depth rejection).
