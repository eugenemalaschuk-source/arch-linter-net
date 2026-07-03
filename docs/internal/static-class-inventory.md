# Static Class Inventory (#154)

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

Issue #154 asks for every production `static class` under `src/` to be classified so static code stays limited to intentional pure helpers, extension methods, constants, or compatibility facades, while services/orchestrators that own behavior, state, or collaborators become instance-based and composition-root managed. This document is that inventory. It also seeds guardrail candidates for #142's self-policy work.

35 production `static class` declarations existed under `src/` (none in `src/ArchLinterNet.Unity/`) before #158's change converted `ArchitectureContractExecutor`, leaving 34 classified below. #159 converted the remaining 14 production service/orchestrator/scanner/resolver/parser/loader classes tracked in section (e) below (2 of which held hidden `static Lazy<T>` global state); the 20 pure-helper/extension/facade classes in sections (a), (b), and (d) are unchanged and remain intentionally static.

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

## (e) Production service/orchestrator — converted

These owned real logic; all are now instance classes. #158 converted the one the issue named explicitly (`ArchitectureContractExecutor`); #159 converted the remaining 14, none of which remain a `static class` declaration.

| File | Class | Status | Notes |
|---|---|---|---|
| `Core/Execution/ArchitectureContractExecutor.cs` | `ArchitectureContractExecutor` | **Converted** (#158) | `IArchitectureContractExecutor` / instance class, registered `AddSingleton` in `AddArchLinterNetCore()`, injected into `ArchitectureValidationApplicationService` and `ArchitectureBaselineApplicationService` |
| `Core/Resolution/ArchitectureRepositoryRootLocator.cs` | *(deleted)* | **Converted** (#159) | Logic and the walk-up-from-`BaseDirectory` auto-discovery algorithm moved into `ArchitectureRepositoryRootResolver` (`IArchitectureRepositoryRootResolver`, registered `AddSingleton`); the `private static readonly Lazy<string> _root` hidden-global-state field is gone — the resolver is already a DI singleton, so per-app "resolved once" semantics come from the container, not a static field |
| `Core/Contracts/ArchitectureContractLoader.cs` | *(deleted)* | **Converted** (#159) | Logic moved into `ArchitecturePolicyDocumentLoader` (`IArchitecturePolicyDocumentLoader`, registered `AddSingleton`); the `private static readonly Lazy<ArchitectureContractDocument> _document` hidden-global-state field is gone. The parameterless `Load()`/`LoadFromRepositoryRoot` auto-discovery entry points had no in-repo callers and were dropped rather than ported forward. `NormalizeToContractId` remains a `public static` pure-utility method on the (now non-static) `ArchitecturePolicyDocumentLoader` class |
| `Core/Discovery/ArchitectureProjectDiscovery.cs` | *(deleted)* | **Converted** (#159) | Logic moved into `ArchitectureProjectDiscoveryService` (`IArchitectureProjectDiscoveryService`, registered via an `AddSingleton` factory that resolves the two parsers below from the container) |
| `Core/Execution/ArchitectureAssemblyResolver.cs` | *(deleted)* | **Converted** (#159) | Logic moved into `ArchitectureAssemblyResolutionService` (`IArchitectureAssemblyResolutionService`, registered `AddSingleton`) |
| `Core/Contracts/ArchitectureBaselineLoader.cs` | *(deleted)* | **Converted** (#159) | Folded into `ArchitectureBaselineLoadingService` (`IArchitectureBaselineLoadingService`, registered `AddSingleton`) as `internal` instance methods using the service's own injected `IArchitectureFileSystem` — not a standalone `static class`, and its only production caller |
| `Core/Contracts/ArchitectureBaselineMerger.cs` | *(deleted)* | **Converted** (#159) | Folded into `ArchitectureBaselineLoadingService` alongside the loader above, including the nested `ContractGroupMerger` helper, for the same single-caller reason |
| `Core/Contracts/ArchitectureBaselineGenerator.cs` | Converted (#159) | `IArchitectureBaselineGenerator` / `internal sealed class`, registered `AddSingleton`, injected into `ArchitectureBaselineApplicationService` |
| `Core/Reporting/ArchitectureDiagnosticFormatter.cs` | Converted (#159) | `IArchitectureDiagnosticFormatter` / `public sealed class` (public because it's consumed cross-assembly by the CLI and Testing library, which construct it directly with `new()` rather than through DI, matching the compatibility-facade pattern in section (d)); also registered `AddSingleton` for composition-root consumers |
| `Core/Discovery/ArchitectureSolutionParser.cs` | Converted (#159) | `IArchitectureSolutionParser` / `internal sealed class`, registered `AddSingleton` in `AddArchLinterNetCore()` and constructor-injected into `ArchitectureProjectDiscoveryService` |
| `Core/Discovery/ArchitectureProjectFileParser.cs` | Converted (#159) | `IArchitectureProjectFileParser` / `internal sealed class`, registered `AddSingleton` in `AddArchLinterNetCore()` and constructor-injected into `ArchitectureProjectDiscoveryService` |
| `Core/Scanning/ArchitectureAsmdefScanner.cs` | Converted (#159) | `IArchitectureAsmdefScanner` / `internal sealed class`, registered `AddSingleton`. `ArchitectureAnalysisSession` (a per-run domain object outside the DI graph, per `core-composition-root`'s own container-API boundary) constructs its own instance directly (`new ArchitectureAsmdefScanner()`) rather than receiving it via constructor injection — the class holds no mutable state, only an immutable `const` root, so direct instantiation is behavior- and cost-neutral |
| `Core/Scanning/ArchitectureSourceScanner.cs` | Converted (#159) | `IArchitectureSourceScanner` / `internal sealed class`, registered `AddSingleton`; consumed via direct instantiation from `ArchitectureAnalysisSession` for the same reason as the asmdef scanner above |
| `Core/Scanning/ArchitectureExternalDependencyIlScanner.cs` | Converted (#159) | `IArchitectureExternalDependencyIlScanner` / `internal sealed class`, registered `AddSingleton`; consumed via direct instantiation from `ArchitectureAnalysisSession`. Its `_opCodes` IL-opcode lookup table remains a `private static readonly` field — a precomputed immutable constant, not mutable global state |
| `Core/Scanning/ArchitectureIlMethodBodyScanner.cs` | Converted (#159) | `IArchitectureIlMethodBodyScanner` / `internal sealed class`, registered `AddSingleton`; same direct-instantiation and `_opCodes` treatment as the external-dependency IL scanner above |

## Guardrail candidates for #142

- Forbid new `static class` declarations under `src/ArchLinterNet.Core/**` that are not one of: extension method container (`this` first parameter on every public method), a class whose only fields are `const`/`static readonly` value types or immutable collections (constants/options holder), or a class explicitly listed in the (a)/(d) tables above.
- Flag any `static readonly Lazy<T>` field in production code as a hidden-global-state smell requiring explicit review. As of #159, zero such fields remain outside the two intentional, already-documented compatibility facades (`ArchitectureValidationService`, `ArchitectureBaselineService`); a new one appearing anywhere else is a regression.
- Require any new orchestrator/scanner/resolver/finder/parser/loader class to be added as an instance class registered in `ServiceCollectionExtensions.AddArchLinterNetCore()` (or, for a stateless per-run collaborator consumed only by a non-DI-composed domain object such as `ArchitectureAnalysisSession`, an instance class constructed directly at the point of use — see the scanner entries in section (e) above), not as a new `static class`.
- As of #159, section (e) is empty of "follow-up candidate" entries — every class in this repo classified as a production service/orchestrator/scanner/resolver/parser/loader is instance-based. A new static class matching that classification should be treated as new debt, not a continuation of pre-existing debt.
