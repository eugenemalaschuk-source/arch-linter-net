## Why

PR #184 (issue #58) shipped `assembly_dependency` and `assembly_allow_only` as direct-reference-only contract families, per an explicit MVP scope decision. Two follow-up hardening gaps remained: `assembly_dependency` violation evidence reused `Assembly.Location` (a filesystem path, environment-dependent and not useful architecture evidence), and neither family's YAML model made the direct-only decision explicit enough to prevent a future author or agent from assuming hidden transitive behavior, unlike the namespace-level `dependency`/`allow_only` contracts which already expose `dependency_depth`.

## What Changes

- `assembly_dependency` violation evidence changes from the source assembly's `Assembly.Location` to a deterministic `"{Source} -> {Forbidden}"` string.
- Both `ArchitectureAssemblyDependencyContract` and `ArchitectureAssemblyAllowOnlyContract` gain an optional `dependency_depth` field (reusing the existing `DependencyDepthMode` enum), defaulting to `direct`.
- Declaring `dependency_depth: transitive` on either family fails policy loading with an actionable error identifying the contract and the family — transitive assembly-reference-path resolution remains unimplemented and out of scope for this change.
- No behavior change to `assembly_allow_only`'s direct-only, declared-assembly-scoped semantics; no change to `assembly_independence`, `asmdef`, or namespace-level `dependency`/`allow_only` contracts.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `assembly-dependency-contracts`: violation evidence format changes to `Source -> Forbidden`; adds optional `dependency_depth` field with fail-fast rejection of `transitive`.
- `assembly-allow-only-contracts`: adds optional `dependency_depth` field with fail-fast rejection of `transitive`; no change to existing allow-only evaluation semantics.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: `DependencyDepth` property on both models.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: `dependency_depth: transitive` rejection in `ValidateAssemblyDependencyContracts`/`ValidateAssemblyAllowOnlyContracts`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyDependency.cs`: evidence format change in `CheckAssemblyDependencyContract`.
- `schema/dependencies.arch.schema.json`: `dependency_depth` property on both `$defs` entries.
- `docs/contracts/assembly-dependency.md`, `docs/reference/yaml-schema.md`, `docs/policy-format/supported-capabilities.md`, `docs/ai/policy-authoring-guide.md`.
- Tests under `tests/ArchLinterNet.Core.Tests/` (evidence assertion, depth-default, explicit-direct, transitive-rejection cases).
