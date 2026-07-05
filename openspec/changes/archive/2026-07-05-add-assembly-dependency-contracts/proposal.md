## Why

`strict_independence`/`audit_independence` and `strict_assembly_independence`/`audit_assembly_independence` (#51) can only express mutual "these must never reference each other" relationships between namespace/layer globs or compiled assemblies. Real .NET solutions need *directional* assembly/project rules — `MyApp.Domain` must never reference `MyApp.Infrastructure`, application assemblies may depend on abstraction assemblies but not concrete adapters, feature assemblies may only reference an approved allow-list — none of which mutual independence can express, and namespace-level `strict`/`strict_allow_only` contracts are an imperfect proxy when project/assembly boundaries don't cleanly map to namespace prefixes. Issue #58 asks for a dedicated, directional assembly/project-level contract family, built on the existing contract-catalog + handler-registry seam established by `assembly_independence` (#51) and `contract-handler-execution` (#137), with no `ArchitectureContractExecutor` changes.

## What Changes

- New contract family `assembly_dependency` (YAML groups `strict_assembly_dependency`/`audit_assembly_dependency`): a single named source assembly must not directly reference any assembly in a `forbidden` list, mirroring `ArchitectureDependencyContract`'s `source`/`forbidden` shape but at the assembly axis instead of the namespace axis.
- New contract family `assembly_allow_only` (YAML groups `strict_assembly_allow_only`/`audit_assembly_allow_only`): a single named source assembly may only directly reference assemblies in an `allowed` list (plus itself); any other direct reference to a *declared* target assembly is a violation, mirroring `ArchitectureAllowOnlyContract`.
- Both new models (`ArchitectureAssemblyDependencyContract`, `ArchitectureAssemblyAllowOnlyContract`) registered through the existing contract-catalog + handler-registry seam, added to `ArchitectureContractGroups`, `ArchitecturePolicyDocumentLoader` duplicate-ID validation, and two new `IArchitectureContractHandler` implementations + DI registrations — no `ArchitectureContractExecutor` changes.
- Direct-reference detection only via `Assembly.GetReferencedAssemblies()`, matched by simple name against `Context.TargetAssemblies` — same decision #51 made for `assembly_independence`. No transitive assembly-reference-path walking in this change; documented explicitly as a scope decision, not an oversight.
- New loader validation: every assembly name referenced by an `assembly_dependency`/`assembly_allow_only` contract (`source`, `forbidden`, `allowed`) must appear in `analysis.target_assemblies`, mirroring the existing `assembly_independence` loader check.
- Reuses `ArchitectureViolation`/`ArchitectureIgnoredViolation` — no new violation type. Diagnostics are distinguished from namespace/layer dependency violations, `assembly_independence` violations, and `.asmdef` violations by contract family/group name (`assembly_dependency`/`assembly_allow_only` vs. `dependency`/`allow_only`/`assembly_independence`/`asmdef`), exactly as `assembly_independence` is already distinguished from `independence` and `asmdef`.
- Ordered assembly/project layer contracts and assembly/project cycle detection are explicitly **out of scope** for this change (see Non-Goals) and left as follow-up work, matching how `layer`/`cycle` are already separate families from `dependency`/`allow_only` at the namespace level.
- JSON schema, docs (new `docs/contracts/assembly-dependency.md`, contract index, policy-format reference, AI-facing guidance, README), and a sample policy updated to cover both new families and distinguish them from `assembly_independence` and `.asmdef`.

## Capabilities

### New Capabilities
- `assembly-dependency-contracts`: strict/audit contracts that forbid one named .NET assembly from directly referencing one or more other named assemblies (directional forbidden-reference check at the assembly axis).
- `assembly-allow-only-contracts`: strict/audit contracts that restrict a named .NET assembly to only directly referencing an explicitly allowed set of other declared assemblies.

### Modified Capabilities
(none — existing `dependency-contracts`, `allow-only-contracts`, `assembly-independence-contracts`, and `asmdef-contracts` behavior is unchanged; this change only adds two new, additive contract families alongside them.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitectureAssemblyDependencyContract` and `ArchitectureAssemblyAllowOnlyContract` models, new `StrictAssemblyDependency`/`AuditAssemblyDependency`/`StrictAssemblyAllowOnly`/`AuditAssemblyAllowOnly` groups.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: new assembly-name-resolvability validation for both families; duplicate-id group wiring.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyDependency.cs` (new): `CheckAssemblyDependencyContract`, `CheckAssemblyAllowOnlyContract`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: four new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `AssemblyDependencyContractHandler`, `AssemblyAllowOnlyContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: two new DI registrations.
- `schema/dependencies.arch.schema.json`: new array properties and `$defs.assemblyDependencyContract`/`$defs.assemblyAllowOnlyContract`.
- `docs/contracts/assembly-dependency.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`, `README.md`, `mkdocs.yml`.
- `samples/policies/modular-monolith.yml`: new examples.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
