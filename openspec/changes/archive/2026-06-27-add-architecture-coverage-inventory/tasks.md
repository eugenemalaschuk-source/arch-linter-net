## 1. Inventory model

- [x] 1.1 Add `ArchitectureCoverageNamespaceEntry`, `ArchitectureCoverageDependencyEdge`, and `ArchitectureCoverageInventory` record/class shapes in `src/ArchLinterNet.Core/Execution/ArchitectureCoverageInventory.cs`.
- [x] 1.2 Implement `ArchitectureCoverageInventory.Build(ArchitectureContractDocument document, ArchitectureAnalysisSession session, ProjectDiscoveryResult? discoveryResult)` as a static factory.

## 2. Namespace and representative-type collection

- [x] 2.1 Group `session.TypeIndex`'s loaded types by namespace, sort namespaces with `StringComparer.Ordinal`.
- [x] 2.2 Pick the alphabetically-first type's full name per namespace as the representative type.

## 3. Layers and templates

- [x] 3.1 Capture `document`'s declared layers, preserving each `ArchitectureLayer`'s namespace, namespace suffix, and external flag (`ArchitectureCoverageLayerEntry` list), in document order.
- [x] 3.2 Capture `LayerTemplateExpander.Expand(document.LayerTemplates)` output as-is, preserving `Exhaustive`.

## 4. Project/assembly facts

- [x] 4.1 Store the passed `ProjectDiscoveryResult` verbatim when present; expose as absent (nullable) when not provided. Carry the resolved `ProjectDiscoveryResult` through `ArchitectureAnalysisContext`/`ArchitectureRunnerFactory.BuildRunner()` so it reaches the inventory through the real runner/session path, not just direct unit construction.

## 5. Dependency edges

- [x] 5.1 For each namespace-mapped type, resolve direct references via `session.ReferenceGraph.GetReferencedTypes(type)`.
- [x] 5.2 Map referenced types to their namespace, exclude self-edges, deduplicate, and sort edges by source then target with `StringComparer.Ordinal`.
- [x] 5.3 Make edge computation lazy so it only runs when a consumer accesses the edges collection.

## 6. Opt-in wiring

- [x] 6.1 Expose inventory construction as a lazily-invoked accessor (e.g. a method on `ArchitectureAnalysisSession` or a separate factory call site) that is never invoked by existing contract execution paths, so no existing run builds it implicitly.

## 7. Tests

- [x] 7.1 Add `ArchitectureCoverageInventoryTests.cs` under `tests/ArchLinterNet.Core.Tests/` using realistic fixtures (existing sample policies/test assemblies).
- [x] 7.2 Test deterministic namespace ordering and representative-type selection across repeated builds.
- [x] 7.3 Test dependency-edge deduplication, self-edge exclusion, and sort order.
- [x] 7.4 Test exhaustive layer template expansion is preserved unchanged in the inventory.
- [x] 7.5 Test project/assembly facts are present when discovery result is supplied and absent when it is not.
- [x] 7.6 Test that validating a policy without coverage contracts does not construct the inventory (existing behavior unaffected) — verified via a counting/throwing test seam on the normal `ArchitectureValidationService.Validate` path, not just by calling the accessor directly.
- [x] 7.7 Test that declared layers preserve `NamespaceSuffix` and `External` (not just `Namespace`), and that the inventory's project/assembly facts reach a session built through `ArchitectureRunnerFactory.BuildRunner()`.

## 8. Validation

- [x] 8.1 Run `make fmt`.
- [x] 8.2 Run `make acceptance` (no `task` runner in this environment) and fix any failures — 437+1+50 tests passed.

## 9. Spec sync and archive

- [ ] 9.1 Run `openspec validate --all` after archiving to confirm specs are consistent.
- [ ] 9.2 Run `openspec archive add-architecture-coverage-inventory` once implementation, tests, and validation pass.
