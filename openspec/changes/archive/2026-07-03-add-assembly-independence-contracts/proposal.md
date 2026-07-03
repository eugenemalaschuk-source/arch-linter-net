## Why

`strict_independence`/`audit_independence` contracts enforce mutual independence between namespace/layer glob patterns, but cannot express "these two compiled .NET assemblies must never reference each other" directly. Namespace globs are an imperfect proxy for assembly ownership when feature assemblies, plugin packages, or `Domain.Abstractions`/`Infrastructure.*` splits don't cleanly map to namespace prefixes, so accidental project/assembly references can slip past CI even when the namespace-level policy passes. Issue #51 asks for a dedicated assembly/project-level independence contract family, built on the post-#69 contract catalog/handler pipeline instead of new manual loops in the CLI, public validator, or `ArchitectureContractRunner`.

## What Changes

- New contract family `assembly_independence` (YAML groups `strict_assembly_independence` / `audit_assembly_independence`), registered through the existing contract-catalog + handler-registry seam — no `ArchitectureContractExecutor` changes.
- New model `ArchitectureAssemblyIndependenceContract` (`name`, `id`, `assemblies: string[]`, `ignored_violations`, `reason`) on `ArchitectureContractGroups`, mirroring `ArchitectureIndependenceContract`'s shape with `assemblies` instead of `layers`.
- New session logic evaluates every ordered pair of distinct assembly names declared in the contract (in YAML declaration order), resolves each against `Context.TargetAssemblies` by simple name, and checks `Assembly.GetReferencedAssemblies()` for a direct reference from the source assembly to the forbidden assembly. Direct references only; transitive resolution is out of scope for this change.
- Reuses the existing `ArchitectureViolation` record and `ArchitectureIgnoredViolation` ignore mechanism — no new violation or ignore-rule type.
- New loader validation: every assembly name listed in a strict/audit assembly-independence contract must appear in `analysis.target_assemblies`, else a clear config-time error instead of a silent runtime skip. Wired into duplicate-contract-id validation; deliberately NOT added to the `rule_input` coverage layer-bearing contract-id whitelist (assembly names are not `document.Layers` keys).
- New `AssemblyIndependenceContractHandler` and one DI registration line, mirroring `IndependenceContractHandler`.
- JSON schema, docs (contract page, contract index, policy-format reference, AI-facing guidance, README), and a sample policy are all updated to cover the new family and clearly distinguish it from namespace/layer independence and from Unity `.asmdef` checks (both unchanged).

## Capabilities

### New Capabilities
- `assembly-independence-contracts`: strict/audit contracts that verify a set of named .NET assemblies do not directly reference one another, complementing namespace/layer independence with a compiled-assembly-level check.

### Modified Capabilities
(none — existing `independence-contracts` and `asmdef-contracts` behavior is unchanged; this change only adds a new, additive contract family alongside them.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitectureAssemblyIndependenceContract` model, new `StrictAssemblyIndependence`/`AuditAssemblyIndependence` groups.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: new assembly-name-resolvability validation; duplicate-id group wiring.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyIndependence.cs` (new): `CheckAssemblyIndependenceContract`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: two new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `AssemblyIndependenceContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: one new DI registration.
- `schema/dependencies.arch.schema.json`: new array properties and `$defs.assemblyIndependenceContract`.
- `docs/contracts/assembly-independence.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`, `README.md`, `mkdocs.yml`.
- `samples/policies/modular-monolith.yml`: new example.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
