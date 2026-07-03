# Static Class Inventory (#154)

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

Issue #154 asks for every production `static class` under `src/` to be classified so static code stays limited to intentional pure helpers, extension methods, constants, or compatibility facades, while services/orchestrators that own behavior, state, or collaborators become instance-based and composition-root managed. This document is that inventory. It also seeds guardrail candidates for #142's self-policy work.

35 production `static class` declarations existed under `src/` (none in `src/ArchLinterNet.Unity/`) before this change. One (`ArchitectureContractExecutor`) has been converted to an instance-based, DI-registered service as part of this change, leaving 34 static classes classified below.

## (a) Pure helper / deterministic mapper — allowed static

No state, no I/O side effects beyond what's passed in as parameters (e.g. an injected `IArchitectureFileSystem` seam).

| File | Class | What it does |
|---|---|---|
| `Core/Reporting/ArchitectureDiagnosticMapper.cs` | `ArchitectureDiagnosticMapper` | Maps `ArchitectureViolation` records to typed diagnostic DTOs |
| `Core/Contracts/ConditionSetResolver.cs` | `ConditionSetResolver` | Resolves a named condition set to preprocessor symbols from a document |
| `Core/Discovery/ProjectPathGlob.cs` | `ProjectPathGlob` | Glob pattern matcher for project paths |
| `Core/Scanning/ArchitectureReferenceScanner.cs` | `ArchitectureReferenceScanner` | Reflects a type's interfaces/base/fields/props/methods for referenced types |
| `Core/Scanning/ArchitectureForbiddenCallMatcher.cs` | `ArchitectureForbiddenCallMatcher` | Normalizes and matches forbidden-call patterns against symbols |
| `Core/Scanning/ArchitectureSiblingGraphBuilder.cs` | `ArchitectureSiblingGraphBuilder` | Groups types under an ancestor namespace into sibling buckets |
| `Core/Scanning/ArchitectureTypeScanner.cs` | `ArchitectureTypeScanner` | Finds types in assemblies matching namespace/layer predicates |
| `Core/Scanning/ArchitectureCycleDetector.cs` | `ArchitectureCycleDetector` | DFS cycle detection over a dependency graph |
| `Core/Scanning/ArchitectureTypeNames.cs` | `ArchitectureTypeNames` | Safe accessors for `Type.Namespace`/`FullName` that swallow load errors |
| `Core/Resolution/ArchitectureLayerResolver.cs` | `ArchitectureLayerResolver` | Resolves layer definitions and matches namespaces against layer glob patterns |
| `Core/Resolution/ArchitectureIgnoreMatcher.cs` | `ArchitectureIgnoreMatcher` | Matches source/reference pairs against ignore patterns |
| `Core/Resolution/ArchitectureExternalDependencyResolver.cs` | `ArchitectureExternalDependencyResolver` | Resolves external dependency groups and matches type/namespace prefixes |
| `Core/Execution/ArchitectureExternalDependencyViolationFinder.cs` | `ArchitectureExternalDependencyViolationFinder` | Builds violations for types referencing forbidden external dependencies |
| `Core/Execution/ArchitectureNamespaceViolationFinder.cs` | `ArchitectureNamespaceViolationFinder` | Builds violations for types referencing forbidden namespaces |
| `Core/Execution/LayerTemplateExpander.cs` | `LayerTemplateExpander` | Expands layer templates into concrete per-container layer contracts |

## (b) Extension method container — allowed static

| File | Class | What it does |
|---|---|---|
| `Core/Composition/ServiceCollectionExtensions.cs` | `ServiceCollectionExtensions` | `AddArchLinterNetCore()` — the Core composition root itself |

## (c) Constants / options holder — allowed static

None found.

## (d) Compatibility facade delegating to a composed service — allowed static

Each of these builds or delegates to an `ArchitectureEngine`/DI-composed service rather than owning behavior itself.

| File | Class | What it does |
|---|---|---|
| `Cli/Program.cs` | `Program` | CLI entry point; parses args, delegates to the validation/baseline services |
| `Testing/ArchitectureAssertions.cs` | `ArchitectureAssertions` | Entry point returning a fluent `ArchitectureValidationBuilder` |
| `Core/Validation/ArchitectureValidationService.cs` | `ArchitectureValidationService` | Static facade holding a `Lazy<ArchitectureEngine>` built via `ArchitectureEngineBuilder().AddArchLinterNetCore()`; used by `ArchitectureValidator`, CLI, Testing |
| `Core/Validation/ArchitectureBaselineService.cs` | `ArchitectureBaselineService` | Same `Lazy<ArchitectureEngine>` facade pattern, for baseline generation |

## (e) Production service/orchestrator — converted or tracked for follow-up

These own real logic, and several already have a DI-registered wrapper service that forwards to them. Converting all of them in one PR was assessed and rejected as too large for one review (see `openspec/changes/normalize-static-production-services/design.md`); this change converts the one the issue names explicitly and tracks the rest below as follow-up candidates for #142 to encode as guardrails until they're converted.

| File | Class | Status | Notes |
|---|---|---|---|
| `Core/Execution/ArchitectureContractExecutor.cs` | `ArchitectureContractExecutor` | **Converted** (this change) | Now `IArchitectureContractExecutor` / instance class, registered `AddSingleton` in `AddArchLinterNetCore()`, injected into `ArchitectureValidationApplicationService` and `ArchitectureBaselineApplicationService` |
| `Core/Contracts/ArchitectureContractLoader.cs` | `ArchitectureContractLoader` | Follow-up candidate | Has a `private static readonly Lazy<ArchitectureContractDocument> _document` — hidden global-state singleton (the parameterless `Load()` method is unused anywhere in-repo). Already shadow-wrapped by `ArchitecturePolicyDocumentLoader`, a good conversion target |
| `Core/Resolution/ArchitectureRepositoryRootLocator.cs` | `ArchitectureRepositoryRootLocator` | Follow-up candidate | Has a `private static readonly Lazy<string> _root` — hidden global-state singleton. Already shadow-wrapped by `ArchitectureRepositoryRootResolver` |
| `Core/Discovery/ArchitectureProjectDiscovery.cs` | `ArchitectureProjectDiscovery` | Follow-up candidate | Orchestrates solution/project-file parsing and glob filtering; already shadow-wrapped by `ArchitectureProjectDiscoveryService` |
| `Core/Execution/ArchitectureAssemblyResolver.cs` | `ArchitectureAssemblyResolver` | Follow-up candidate | Resolves target assemblies via probing paths/env var; already shadow-wrapped by `ArchitectureAssemblyResolutionService` |
| `Core/Contracts/ArchitectureBaselineLoader.cs` | `ArchitectureBaselineLoader` | Follow-up candidate | Loads and validates baseline YAML documents |
| `Core/Contracts/ArchitectureBaselineMerger.cs` | `ArchitectureBaselineMerger` | Follow-up candidate | Merges baseline ignore entries into policy contract groups; has a nested stateful `ContractGroupMerger` helper |
| `Core/Contracts/ArchitectureBaselineGenerator.cs` | `ArchitectureBaselineGenerator` | Follow-up candidate | Generates and serializes baseline documents from violation candidates |
| `Core/Reporting/ArchitectureDiagnosticFormatter.cs` | `ArchitectureDiagnosticFormatter` | Follow-up candidate | Formats violations/cycles/coverage into human text and CI JSON output |
| `Core/Discovery/ArchitectureSolutionParser.cs` | `ArchitectureSolutionParser` (internal) | Follow-up candidate | Parses `.sln`/`.slnx` files into project paths |
| `Core/Discovery/ArchitectureProjectFileParser.cs` | `ArchitectureProjectFileParser` (internal) | Follow-up candidate | Parses `.csproj` XML for assembly name/target frameworks |
| `Core/Scanning/ArchitectureAsmdefScanner.cs` | `ArchitectureAsmdefScanner` (internal) | Follow-up candidate | Scans Unity asmdef files for forbidden editor/prefix references |
| `Core/Scanning/ArchitectureSourceScanner.cs` | `ArchitectureSourceScanner` (internal) | Follow-up candidate | Roslyn-based source scan for forbidden method-body calls |
| `Core/Scanning/ArchitectureExternalDependencyIlScanner.cs` | `ArchitectureExternalDependencyIlScanner` (internal) | Follow-up candidate | IL-based scan of method bodies for external dependency violations |
| `Core/Scanning/ArchitectureIlMethodBodyScanner.cs` | `ArchitectureIlMethodBodyScanner` (internal) | Follow-up candidate | IL-based scan of method bodies for forbidden call violations |

## Guardrail candidates for #142

- Forbid new `static class` declarations under `src/ArchLinterNet.Core/**` that are not one of: extension method container (`this` first parameter on every public method), a class whose only fields are `const`/`static readonly` value types or immutable collections (constants/options holder), or a class explicitly listed in the (a)/(d) tables above.
- Flag any `static readonly Lazy<T>` field in production code as a hidden-global-state smell requiring explicit review — both current occurrences (`ArchitectureContractLoader._document`, `ArchitectureRepositoryRootLocator._root`) are follow-up conversion candidates above, and the two intentional exceptions (`ArchitectureValidationService`, `ArchitectureBaselineService`) are already documented compatibility facades.
- Require any new orchestrator/scanner/resolver/finder/parser/loader class to be added as an instance class registered in `ServiceCollectionExtensions.AddArchLinterNetCore()`, not as a new `static class`.
