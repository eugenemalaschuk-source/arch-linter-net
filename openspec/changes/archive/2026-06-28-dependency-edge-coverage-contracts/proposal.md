## Why

Namespace, project, and assembly coverage (#143, #146) detect first-party units that bypass architecture policy entirely. They cannot answer a narrower but important question: for units that *are* inside declared layers, is the specific dependency edge between two layers actually governed by a layer, independence, or dependency contract — or does it silently bypass policy because no contract happens to mention that layer pair? `dependency_edge` is the fifth and final coverage scope reserved by `architecture-coverage-model` (#97) and is currently hard-rejected by `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes`. Implementing it closes that gap and completes the coverage scope set (#97-#103).

## What Changes

- Implement `scope: dependency_edge` coverage contracts: declares `between` as a list of declared-layer-name pairs (`[sourceLayer, targetLayer]`), reusing the existing `Between: List<List<string>>` field already defined on `ArchitectureCoverageContract`.
- Add `BuildDependencyEdgeCoverageSummary` and `CheckDependencyEdgeCoverageContract` to `ArchitectureContractRunner.Coverage.cs`, dispatched alongside the existing namespace/project/assembly/rule_input scope switches.
- Define the "governed by" rule: an observed first-party namespace-to-namespace edge whose source namespace resolves to declared layer A and target namespace resolves to declared layer B is `covered` for pair `(A, B)` when any declared dependency, layer, or independence contract already governs that ordered layer pair (see design.md for the precise per-contract-family rule).
- Edges matching a declared `between` pair that are not governed by any contract and not excluded are classified `uncovered`; edges matching an `exclude` entry (`between: [A, B]` plus mandatory `reason`) are `excluded`. Edges whose layer pair is not declared in any coverage contract's `between` list are simply not evaluated (out of scope, not a fourth classification status).
- Remove `dependency_edge` from the unsupported-scope rejection list in `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` now that it is implemented.
- Reuse the existing `Coverage` diagnostic kind/shape: `Scope = "dependency_edge"`, `Status` in `{covered (summary only), excluded, uncovered}`, `RepresentativeUnit` carrying the source/target namespace pair, optional `Reason`.
- Audit mode (`audit_coverage`) reports without failing strict gates; strict mode (`strict_coverage`) fails when configured, exactly mirroring the namespace/project/assembly scopes — `analysis.coverage` severity (`error`/`warn`/`off`) applies identically, with no new severity concept introduced.
- Add fixtures and tests covering: edge governed by a layer contract, edge governed by an independence contract, edge governed by a dependency contract, uncovered edge, excluded edge (with reason), and a mixed-scope policy combining `dependency_edge` coverage with namespace/assembly coverage in the same document.

## Capabilities

### New Capabilities
- `dependency-edge-coverage-contracts`: Detects first-party namespace-to-namespace dependency edges, scoped by declared layer pairs via `between`, that are not governed by any declared layer, independence, or dependency contract, classifying each declared pair's observed edges as `covered`, `excluded`, or `uncovered`.

### Modified Capabilities
(none — `architecture-coverage-model` already reserved `scope: dependency_edge` and the `Coverage` diagnostic kind; this change implements against that existing design without altering its requirements)

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.Coverage.cs`: new scope-dispatch branches and two new private methods.
- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`: `dependency_edge` removed from the unsupported-scope list.
- `src/ArchLinterNet.Core/Execution/ArchitectureCoverageInventory.cs`: reuse existing `DependencyEdges`/`BuildDependencyEdges()` — no changes expected, only consumption.
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractLoader.cs`: extend coverage scope-specific field validation to require `between` (non-empty, declared-layer-name pairs) for `scope: dependency_edge` and reject `roots`/`contract_ids` on that scope (mirroring existing scope-exclusivity validation).
- `tests/ArchLinterNet.Core.Tests/`: new `DependencyEdgeCoverageFixtures.cs`, `DependencyEdgeCoverageContractTests.cs`.
- No changes to CLI argument parsing, baseline generation, or diagnostic formatting — both already handle the generic `Coverage` diagnostic kind and `coverage_summary` shape.
