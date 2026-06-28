## 1. Loader validation

- [x] 1.1 Extend `ArchitectureContractLoader`'s coverage scope-specific field validation to add a `dependency_edge` branch: require non-empty `between`, each pair has exactly two non-empty entries, every entry names a layer declared in `document.Layers`, and reject `roots`/`contract_ids` when `scope == "dependency_edge"`.
- [x] 1.2 Add loader-level unit tests for: missing/empty `between`, a `between` pair referencing an undeclared layer, and `roots` declared alongside `scope: dependency_edge`.

## 2. Governance lookup

- [x] 2.1 Add a private helper on `ArchitectureContractRunner` (e.g. `IsPairGoverned(string sourceLayer, string targetLayer)`) that checks, across `BuildAllDescriptors()` (or the equivalent already-loaded contract collections), whether any `ArchitectureDependencyContract` has `Source == sourceLayer` and `Forbidden` containing `targetLayer`, any `ArchitectureLayerContract.Layers` contains both names, or any `ArchitectureIndependenceContract.Layers` contains both names.
- [x] 2.2 Add a private helper to resolve which declared layer (if any) a namespace matches, reusing `ArchitectureLayerResolver.MatchesNamespace` against `_document.Layers` (mirrors how `IsCoveredByDeclaredLayers` resolves matches).

## 3. Coverage summary and checking

- [x] 3.1 Add `BuildDependencyEdgeCoverageSummary(ArchitectureCoverageContract contract)` to `ArchitectureContractRunner.Coverage.cs`: for each declared pair in `contract.Between`, filter `inventory.DependencyEdges` to edges whose source/target namespaces resolve to that pair's layers, classify each edge as `excluded` (matches an `exclude` entry's `between`), `covered` (pair is governed), or `uncovered`, and aggregate into `ArchitectureCoverageSummaryCounts`.
- [x] 3.2 Wire `BuildDependencyEdgeCoverageSummary` into `BuildCoverageSummary`'s scope dispatch (alongside `rule_input`/`assembly`/`project`/namespace).
- [x] 3.3 Add `CheckDependencyEdgeCoverageContract(ArchitectureCoverageContract contract)`: same filtering/classification as 3.1, but emit one `ArchitectureViolation` (kind `"uncovered dependency edge"`) per uncovered observed edge instance, carrying source namespace, target namespace, and a representative source type as evidence; respect `IgnoredViolations`/`IsIgnored` and unmatched-ignore collection exactly as the other scope checkers do.
- [x] 3.4 Wire `CheckDependencyEdgeCoverageContract` into `CheckCoverageContract`'s scope dispatch.
- [x] 3.5 Remove `dependency_edge` from the unsupported-scope list/message in `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` (and update its error message wording for any remaining unsupported scopes).

## 4. Fixtures and tests

- [x] 4.1 Create `tests/ArchLinterNet.Core.Tests/DependencyEdgeCoverageFixtures.cs` with fixture namespaces/types producing: an edge inside a layer-contract-governed pair, an edge inside an independence-contract-governed pair, an edge inside a dependency-contract-governed pair, an edge inside an undeclared (uncovered) pair, and an edge inside an excluded pair.
- [x] 4.2 Create `tests/ArchLinterNet.Core.Tests/DependencyEdgeCoverageContractTests.cs` following the `ProjectAssemblyCoverageContractTests.cs` structure: tests for covered-by-layer-contract, covered-by-independence-contract, covered-by-dependency-contract, uncovered, excluded-with-reason, and a mixed-scope policy combining `dependency_edge` coverage with a namespace or assembly coverage contract in the same document.
- [x] 4.3 Add a test verifying a layer pair not declared in any `between` list produces no finding and is excluded from the coverage summary counts.
- [x] 4.4 Add a test verifying `audit_coverage` reports uncovered edges without failing strict validation, and `strict_coverage` fails when `analysis.coverage` is `error` (default).

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `task acceptance:fresh` and fix any failures.
- [x] 5.3 Run `openspec validate --all` after archiving to confirm the rebuilt `dependency-edge-coverage-contracts` spec and unaffected specs all pass.
