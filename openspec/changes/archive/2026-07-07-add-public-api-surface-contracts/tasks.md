## 1. Contract model and schema

- [x] 1.1 Add `ArchitecturePublicApiSurfaceContract` (`Name`/`Id`/`Assemblies`/`DeclaredApi`/`ForbidPublicConstantsUnlessDeclared`/`AllowedPublicConstants`/`IgnoredViolations`/`Reason`) to `ArchitectureContractModels.cs`
- [x] 1.2 Add `StrictPublicApiSurface`/`AuditPublicApiSurface` lists to `ArchitectureContractGroups`, wire into `EnumerateStrict`/`EnumerateAudit`
- [x] 1.3 Add `publicApiSurfaceContract` def and `strict_public_api_surface`/`audit_public_api_surface` array properties to `schema/dependencies.arch.schema.json`

## 2. Signature normalization and scanning

- [x] 2.1 Add `ArchitecturePublicApiSurfaceScanner` (new file in `Scanning/`): enumerate exported types (`public`, or `protected`/`protected internal` nested inside an already-exported enclosing chain) via `ArchitectureTypeScanner.GetLoadableTypes`; enumerate `DeclaredOnly` exported members (ctor/method excluding accessor methods/property/field including const/event), filtering out compiler-generated members
- [x] 2.2 Implement deterministic signature normalization (`<kind> <FullyQualifiedName>[(<param types>)][: <member type>]`) using `Type.FullName`/`MemberInfo` reflection data, with positional generic type-parameter naming for generic types/methods
- [x] 2.3 Add defensive handling for reflection failures consistent with `ArchitectureTypeScanner.GetLoadableTypes`

## 3. Execution wiring

- [x] 3.1 Implement `CheckPublicApiSurfaceContract` in new `ArchitectureAnalysisSession.PublicApiSurface.cs`: resolve target assemblies from `Assemblies`, scan exported surface via `ArchitecturePublicApiSurfaceScanner`, compare against `DeclaredApi` set, emit one `ArchitectureViolation` per undeclared signature
- [x] 3.2 Implement the `forbid_public_constants_unless_declared` check: for every exported `const` field, if the flag is true and its fully-qualified member name is not in `AllowedPublicConstants`, emit a forbidden-constant violation (independent of whether the const's full signature is in `DeclaredApi`)
- [x] 3.3 Add `UndeclaredApiSignature`/`ForbiddenPublicConstant` optional fields to `ArchitectureViolation`
- [x] 3.4 Add `ArchitectureDiagnosticKind.PublicApiSurface` and new `PublicApiSurfaceDiagnostic` record in `Model/`
- [x] 3.5 Add mapping branch in `ArchitectureDiagnosticMapper.FromViolation` dispatching to `PublicApiSurfaceDiagnostic` when `UndeclaredApiSignature` is set
- [x] 3.6 Add human-text and CI-JSON rendering for `PublicApiSurfaceDiagnostic` in `ArchitectureDiagnosticFormatter`
- [x] 3.7 Add `PublicApiSurfaceContractHandler` (family `public_api_surface`) to `ArchitectureContractHandlers.cs`
- [x] 3.8 Register the new handler in `ServiceCollectionExtensions.cs` (if handlers are DI-registered individually; otherwise confirm auto-registration covers it)
- [x] 3.9 Add two new `AddGroup` calls (`strict_public_api_surface`, `audit_public_api_surface`, family `public_api_surface`) to `ArchitectureContractCatalog.Build`
- [x] 3.10 Add the two new groups to `ArchitecturePolicyDocumentLoader`'s duplicate-ID validation list, plus a new validation rejecting a `public_api_surface` contract with an empty/missing `assemblies` list

## 4. Tests

- [x] 4.1 Create `PublicApiSurfaceContractTests.cs` and fixture types covering: clean declared API (no violations), accidental public type, accidental public member (method/property/field/event), public constant undeclared (default behavior), protected member treated as exported, nested type visibility (both exported-via-exported-parent and hidden-via-internal-parent cases), generic type and generic method signatures
- [x] 4.2 Add `forbid_public_constants_unless_declared` coverage: declared-but-still-forbidden constant, `allowed_public_constants` exemption passes
- [x] 4.3 Add strict-failure and audit-only-reporting tests for `public_api_surface`
- [x] 4.4 Add `ignored_violations` suppression + unmatched-ignore tracking test
- [x] 4.5 Add `CheckConfiguration_...` test: a `public_api_surface` contract with an empty/missing `assemblies` list is rejected at load time
- [x] 4.6 Confirm full existing test suite still passes unchanged

## 5. Docs

- [x] 5.1 Add `docs/contracts/public-api-surface.md` (Groups, signature-normalization grammar with worked examples, `forbid_public_constants_unless_declared` semantics with worked example, Example YAML, When to use, Semantics, Non-goals)
- [x] 5.2 Update `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`
- [x] 5.3 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`
- [x] 5.4 Update `archlinternet.capabilities.json` with the new contract family entry

## 6. Validation

- [x] 6.1 Run `make fmt`
- [x] 6.2 Run `make acceptance`
- [x] 6.3 Fix any failures and rerun until green — 5 `ArchLinterNet.Core.Tests` failures (`ArchitectureProjectDiscoveryServiceFakeFileSystemTests` x2, `ArchitectureRepositoryRootResolverTests` x2, `ArchitectureSourceScannerFakeSeamTests` x1) and `make lint-docs`'s `python3` spawn error are pre-existing Windows-environment issues, confirmed identical on a clean `main` checkout (`git stash -u`). All other suites are green: `ArchLinterNet.Cli.Tests` 80/80, `ArchLinterNet.Unity.Tests` 3/3, all 14 new `PublicApiSurfaceContractTests` pass, full solution builds clean, `dotnet format --verify-no-changes` clean, self-policy `dotnet run ... --mode strict` passes with full coverage.
