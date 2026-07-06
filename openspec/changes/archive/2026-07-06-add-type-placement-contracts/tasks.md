## 1. Contract model and schema

- [x] 1.1 Add `ArchitectureTypeMatcher` (`NameSuffix`/`NamePrefix`/`Namespace`/`Layer`/`BaseType`/`ImplementsInterface`/`HasAttribute`) and `ArchitectureTypePlacementContract` (`Name`/`Id`/`TypesMatching`/`MustResideInLayers`/`MustResideInNamespaces`/`MustResideInProjects`/`MustResideInAssemblies`/`RequiredNameSuffix`/`RequiredNamePrefix`/`ForbiddenNameSuffix`/`ForbiddenNamePrefix`/`IgnoredViolations`/`Reason`) to `ArchitectureContractModels.cs`
- [x] 1.2 Add `StrictTypePlacement`/`AuditTypePlacement` lists to `ArchitectureContractGroups`, wire into `EnumerateStrict`/`EnumerateAudit`
- [x] 1.3 Add `typeMatcher` and `typePlacementContract` defs and `strict_type_placement`/`audit_type_placement` array properties to `schema/dependencies.arch.schema.json`

## 2. Execution wiring

- [x] 2.1 Add `ArchitectureTypeRoleMatcher` (new file in `Scanning/`) implementing AND-combination of `types_matching` fields against `ArchitectureTypeIndex.AllTypes()`: name suffix/prefix on `Type.Name`, namespace prefix reuse, layer resolution reuse, base-type chain walk, interface list check, attribute data check (defensive against reflection failures, matching `ArchitectureTypeScanner.GetLoadableTypes`'s handling)
- [x] 2.2 Implement `CheckTypePlacementContract` in new `ArchitectureAnalysisSession.TypePlacement.cs`: select candidate types via the matcher, evaluate placement (union across `must_reside_in_*` lists, resolving `must_reside_in_projects` to assembly names via project discovery) and naming (suffix/prefix checks on `Type.Name`), emit one `ArchitectureViolation` per violating type carrying whichever of the new location/name fields apply
- [x] 2.3 Add `ExpectedTypeLocation`/`ActualTypeLocation`/`ExpectedTypeName`/`ActualTypeName` optional fields to `ArchitectureViolation`
- [x] 2.4 Add `ArchitectureDiagnosticKind.TypePlacement` and new `TypePlacementDiagnostic` record in `Model/`
- [x] 2.5 Add mapping branch in `ArchitectureDiagnosticMapper.FromViolation` dispatching to `TypePlacementDiagnostic` when either new field pair is set
- [x] 2.6 Add human-text and CI-JSON rendering for `TypePlacementDiagnostic` in `ArchitectureDiagnosticFormatter`
- [x] 2.7 Add `TypePlacementContractHandler` (family `type_placement`) to `ArchitectureContractHandlers.cs`
- [x] 2.8 Register the new handler in `ServiceCollectionExtensions.cs`
- [x] 2.9 Add two new `AddGroup` calls (`strict_type_placement`, `audit_type_placement`, family `type_placement`) to `ArchitectureContractCatalog.Build`
- [x] 2.10 Add the two new groups to `ArchitecturePolicyDocumentLoader`'s duplicate-ID validation list, plus a new validation rejecting a `type_placement` contract with a selector but no placement/naming expectation

## 3. Tests

- [x] 3.1 Create `TypePlacementContractTests.cs` covering each selector individually: `name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, and a combined-AND case with two selector fields
- [x] 3.2 Add placement coverage: type outside every declared location is a violation; type inside a declared layer/namespace/assembly passes; `must_reside_in_projects` resolves via project discovery to assembly-name matching
- [x] 3.3 Add naming coverage: missing required suffix/prefix is a violation; present forbidden suffix/prefix is a violation; satisfying naming passes
- [x] 3.4 Add a case where a type fails both placement and naming under one contract, asserting a single violation carries both sets of fields
- [x] 3.5 Add audit-mode test confirming `audit_type_placement` violations are reported without failing strict validation
- [x] 3.6 Add `ignored_violations` suppression + unmatched-ignore tracking test
- [x] 3.7 Add `CheckConfiguration_...` test: a `type_placement` contract with a selector but no placement/naming expectation is rejected at load time
- [x] 3.8 Confirm full existing test suite still passes unchanged

## 4. Docs

- [x] 4.1 Add `docs/contracts/type-placement.md` (Groups, Example, When to use, Semantics, explicit scope note on `must_reside_in_projects` being assembly-name equivalence, Non-goals)
- [x] 4.2 Update `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`
- [x] 4.3 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`
- [x] 4.4 Update `archlinternet.capabilities.json` with the new contract family entry

## 5. Validation

- [x] 5.1 Run `make fmt`
- [x] 5.2 Run `make acceptance`
- [x] 5.3 Fix any failures and rerun until green — fixed `ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder` (caused by this change, added `type_placement` to the expected family order). The remaining 5 `ArchLinterNet.Core.Tests` failures (`ArchitectureProjectDiscoveryServiceFakeFileSystemTests` x2, `ArchitectureRepositoryRootResolverTests` x2, `ArchitectureSourceScannerFakeSeamTests` x1) and `make lint-docs`'s `python3` spawn error are pre-existing environment issues on this Windows machine, confirmed identical on a clean `main` checkout via `git stash -u`. Verified independently: `dotnet test ArchLinterNet.slnx` (same 5 pre-existing failures, all other projects green incl. `ArchLinterNet.Cli.Tests` 80/80 and `ArchLinterNet.Unity.Tests` 3/3), `dotnet format --verify-no-changes` clean, self-policy `dotnet run ... --mode strict` passes with full coverage.
