## 1. Policy model and loading

- [x] 1.1 Add `ArchitectureCompositionContract` to `Contracts/` (`forbidden_apis`, `allowed_only_in_layers/namespaces/projects/assemblies`, `id`, `name`, `reason`, `ignored_violations`) with `StrictComposition`/`AuditComposition` group properties on `ArchitectureContractGroups`, wired into `AllStrict`/`AllAudit`
- [x] 1.2 Add fail-closed load-time validation in `ArchitecturePolicyDocumentLoader` (require non-empty `forbidden_apis`; require at least one non-empty `allowed_only_in_*` list), mirroring interface-implementation validation
- [x] 1.3 Add the composition family to the internal "referenced layer names" collection used for policy-consistency checks

## 2. Scanning

- [x] 2.1 Extract the per-type IL method/constructor scan loop out of `ArchitectureIlMethodBodyScanner` into a method that accepts an explicit `IReadOnlyCollection<Type>` (no behavior change to the existing namespace/layer-scoped public entry point; existing method-body/external-dependency tests must continue to pass unchanged)

## 3. Execution

- [x] 3.1 Add `ArchitectureAnalysisSession.Composition.cs` implementing `CheckCompositionContract`: iterate `TypeIndex.AllTypes()` ordered by fully-qualified name, resolve the allow-list via `ArchitectureLayerResolver.ResolveLayer`/`ResolveProjectAssemblyNames`, skip types satisfying `IsAllowedLocation`, scan remaining types via the extracted IL scan method against `ForbiddenApis` (normalized via `ArchitectureForbiddenCallMatcher.NormalizePatterns`), apply ignores, collect unmatched ignores, order violations deterministically (type, then matched API)
- [x] 3.2 Register `strict_composition`/`audit_composition` in `ArchitectureContractCatalog.Build` (after `interface_implementation`, before `coverage`) and add a handler in `ArchitectureContractHandlers.cs` + registration in `ServiceCollectionExtensions`

## 4. Diagnostics and reporting

- [x] 4.1 Extend `ArchitectureViolation` with composition fields (matched forbidden API, expected composition boundary description); add a `CompositionDiagnostic` record and `ArchitectureDiagnosticKind` entry
- [x] 4.2 Update `ArchitectureDiagnosticMapper` and `ArchitectureDiagnosticFormatter` for the new family

## 5. Schema, capabilities, tooling

- [x] 5.1 Add `strict_composition`/`audit_composition` contract group keys and the `compositionContract` `$defs` entry to `schema/dependencies.arch.schema.json`
- [x] 5.2 Add a capability entry to `archlinternet.capabilities.json`
- [x] 5.3 Verify `tools/scripts/architecture_coverage_report.py` needs no change (derives families from policy coverage contracts, not a hardcoded list) — confirm with its existing tests

## 6. Tests

- [x] 6.1 Add `CompositionContractTestFixtures.cs`: composition-layer types calling DI registration/service-locator APIs (allowed), non-composition types calling the same APIs (forbidden), an ASP.NET-style `IServiceProvider.GetService`/`IServiceCollection.AddSingleton` example, a VContainer/Unity-style container `Resolve`/`Register` example
- [x] 6.2 Add `CompositionContractTests.cs`: allowed composition usage (no violation), forbidden usage outside boundary, namespace-prefix pattern matching, strict failure, audit-only reporting, ignored + unmatched ignores, loader validation errors (missing `forbidden_apis`, missing allow-list), deterministic ordering

## 7. Docs and AI guidance

- [x] 7.1 Add `docs/contracts/composition.md` (including the non-goal statement that runtime DI resolution is not validated); link from `docs/contracts/index.md` and `docs/policy-format/index.md`
- [x] 7.2 Update `docs/policy-format/supported-capabilities.md`, `docs/ai/capabilities.md`, and `docs/ai/policy-authoring-guide.md` with the new family (including a server ASP.NET composition-root example and a Unity/VContainer bootstrap example)
- [x] 7.3 Add composition-root example(s) to sample policies (e.g. `samples/policies/modular-monolith.yml` for the server example, `samples/policies/unity-asmdef-boundaries.yml` for the Unity/VContainer example) and empty-list stubs to `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml`

## 8. Validation

- [x] 8.1 Run `make fmt`
- [x] 8.2 Run `make acceptance` and fix all failures
