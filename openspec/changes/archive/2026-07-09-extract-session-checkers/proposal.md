## Why

`ArchitectureAnalysisSession` still hosts most of its 24 contract-family checking algorithms directly as partial-class methods (`ArchitectureAnalysisSession.*.cs`, ~4,000 lines across 13 files), even though #211 introduced a descriptor-owned checker-registry seam (`contract-handler-execution`) and #212 already pulled configuration-inspection logic out into contributor delegates (`configuration-contributor-registry`). As more families were added in the v0.4.0 wave, the session partials remained the path of least resistance for new family logic, which keeps the session doing double duty as both "shared per-run context/cache owner" and "home for family-specific algorithms." This makes individual families harder to unit-test in isolation (every test must stand up a full `ArchitectureAnalysisSession`) and keeps unrelated families coupled inside the same class via partials. This is issue #213, a behavior-preserving architecture-health follow-up to #211/#212.

## What Changes

- Extract the checking algorithm for three self-contained, single-method contract families — `assembly_independence`, `public_api_surface`, `inheritance` — out of their `ArchitectureAnalysisSession.*.cs` partials and into standalone checker classes under `ArchLinterNet.Core.Execution.Checkers`, each taking explicit constructor/method parameters (contract, target assemblies or type index, an `ArchitectureContractExecutionContext`) instead of the whole session.
- Reduce each of those three `ArchitectureAnalysisSession.Check*Contract` methods to a thin wrapper: contract-selection gate, dangling/rule-input-coverage deferral check, execution-context creation, delegation to the new checker class, and unmatched-ignore collection — all currently-shared session responsibilities that stay on the session.
- Add unit tests that exercise at least one of the new checker classes directly against a minimal/fake `ArchitectureContractExecutionContext`, with no `ArchitectureAnalysisSession` involved, demonstrating the improved testability.
- Update `docs/internal/core-architecture-blueprint.md` to describe the session's narrowed responsibility (per-run context, caches, shared mutable state) versus the `Execution.Checkers` namespace's responsibility (family-specific algorithms), and to note which families still remain inside session partials pending follow-up issues.
- No behavior, public API, CLI output, or `ArchitectureContractChecker`/registry contract changes — the three families keep producing byte-identical diagnostics, baseline candidates, and unmatched-ignore records.
- Out of scope for this change (left as documented follow-up work per the issue's own non-goals): `ArchitectureAnalysisSession.Checking.cs` (11 distinct family algorithms), `ArchitectureAnalysisSession.Coverage.cs`, `ArchitectureAnalysisSession.PolicyConsistency.cs`, and the families whose partials also carry a `ConfigurationContributor` closure or cross-family static helpers such as `IsAllowedLocation`/`ResolveProjectAssemblyNames` (`AssemblyDependency`, `PackageDependency`, `AttributeUsage`, `InterfaceImplementation`, `TypePlacement`, `ProjectMetadata`, and — on closer inspection during implementation — `Composition`, which shares `IsAllowedLocation`/`ResolveProjectAssemblyNames` with `TypePlacement` and was dropped from this change's scope for that reason) — these need larger, separately-scoped extractions.

## Capabilities

### New Capabilities
- `family-checker-extraction`: defines the structural rule that a contract family's checking algorithm lives in a standalone class under `ArchLinterNet.Core.Execution.Checkers`, taking explicit collaborators (contract, target assemblies or type index, execution context) rather than the whole `ArchitectureAnalysisSession`, and that `ArchitectureAnalysisSession.Check*Contract` for such a family is reduced to the shared selection-gate/coverage-deferral/execution-context-creation/unmatched-ignore-collection wrapper. Scoped to the three families covered by this change (`assembly_independence`, `public_api_surface`, `inheritance`); follow-up changes extend it family by family.

### Modified Capabilities

None. `contract-handler-execution` (#211) already specifies that each family's `ArchitectureContractChecker` delegate receives the `ArchitectureAnalysisSession` and dispatches through the descriptor registry — that observable contract is unchanged; only what happens *inside* `ArchitectureAnalysisSession.CheckAssemblyIndependenceContract` (and the other three) changes.

## Impact

- **Affected code**: `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyIndependence.cs`, `.PublicApiSurface.cs`, `.Inheritance.cs`; new files under `src/ArchLinterNet.Core/Execution/Checkers/`.
- **Affected docs**: `docs/internal/core-architecture-blueprint.md`.
- **Affected tests**: new test file(s) under `tests/ArchLinterNet.Core.Tests/` for the extracted checkers; existing `AssemblyIndependenceContractTests.cs`, `PublicApiSurfaceContractTests.cs`, `InheritanceContractTests.cs` must keep passing unmodified since they exercise the public `ArchitectureAnalysisSession.Check*Contract` entry points end-to-end.
- **No dependency, API, or CLI changes.**
