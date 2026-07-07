## 1. Contract model and schema

- [x] 1.1 Add `ArchitectureAttributeUsageContract` (`Name`/`Id`/`Attributes`/`AttributePrefixes`/`AllowedOnlyInLayers`/`AllowedOnlyInNamespaces`/`AllowedOnlyInProjects`/`AllowedOnlyInAssemblies`/`ForbiddenInLayers`/`ForbiddenInNamespaces`/`ForbiddenInProjects`/`ForbiddenInAssemblies`/`IgnoredViolations`/`Reason`) to `ArchitectureContractModels.cs`
- [x] 1.2 Add `StrictAttributeUsage`/`AuditAttributeUsage` lists to `ArchitectureContractGroups`, wire into `EnumerateStrict`/`EnumerateAudit` (before `StrictCoverage`/`AuditCoverage`, which must stay last)
- [x] 1.3 Add `attributeUsageContract` def and `strict_attribute_usage`/`audit_attribute_usage` array properties to `schema/dependencies.arch.schema.json`

## 2. Attribute scanner

- [x] 2.1 Add `ArchitectureAttributeUsageScanner` (new file in `Scanning/`) with `ArchitectureAttributeUsageMatch` record and `GetMatches(Type, attributes, attributePrefixes)`, scanning the type and every declared member (ctor/method excluding accessor methods/property/field/event) regardless of visibility
- [x] 2.2 Implement attribute matching: exact set membership (Ordinal) against `attributes`, or `StartsWith` (Ordinal) against any `attribute_prefixes`, using `ArchitectureTypeNames.SafeFullName` on the attribute type
- [x] 2.3 Add defensive handling for reflection failures (`TypeLoadException`/`FileNotFoundException`/`CustomAttributeFormatException`), consistent with `ArchitecturePublicApiSurfaceScanner`/`ArchitectureTypeRoleMatcher`
- [x] 2.4 Support multiple matches per member (a member with two matching attributes yields two match entries)

## 3. Execution wiring

- [x] 3.1 Implement `CheckAttributeUsageContract` in new `ArchitectureAnalysisSession.AttributeUsage.cs`: resolve allowed/forbidden layers and assembly names (including `ResolveProjectAssemblyNames`), scan every type via `ArchitectureAttributeUsageScanner.GetMatches`, evaluate allow-list/deny-list via the existing `IsAllowedLocation` helper, emit one violation per match that fails either check (preferring "forbidden" when both fail)
- [x] 3.2 Add `MatchedAttribute`/`AttributeUsageKind`/`ExpectedAttributeLocation`/`ActualAttributeLocation` optional fields to `ArchitectureViolation`
- [x] 3.3 Add `ArchitectureDiagnosticKind.AttributeUsage` and new `AttributeUsageDiagnostic` record in `Model/`
- [x] 3.4 Add mapping branch in `ArchitectureDiagnosticMapper.FromViolation` dispatching to `AttributeUsageDiagnostic` when `MatchedAttribute` is set
- [x] 3.5 Add human-text and CI-JSON rendering for `AttributeUsageDiagnostic` in `ArchitectureDiagnosticFormatter`
- [x] 3.6 Add `AttributeUsageContractHandler` (family `attribute_usage`) to `ArchitectureContractHandlers.cs`
- [x] 3.7 Register the new handler in `ServiceCollectionExtensions.cs`
- [x] 3.8 Add two new `AddGroup` calls (`strict_attribute_usage`, `audit_attribute_usage`, family `attribute_usage`) to `ArchitectureContractCatalog.Build`, positioned after `public_api_surface` and before `coverage`
- [x] 3.9 Add the two new groups to `ArchitecturePolicyDocumentLoader`'s duplicate-ID validation list, plus a new `ValidateAttributeUsageContracts` rejecting a contract with no attribute selector (`attributes`/`attribute_prefixes` both empty) or no location expectation (no `allowed_only_in_*`/`forbidden_in_*` populated)
- [x] 3.10 Update `ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder` (and any other order-sensitive test) to include `attribute_usage`

## 4. Tests

- [x] 4.1 Create `AttributeUsageContractTests.cs` with fixture types/attributes covering: clean case (attribute only in allowed layer, no violations); `allowed_only_in_*` misplacement (violation with `AttributeUsageKind == "misplaced"`); `forbidden_in_*` (violation with `AttributeUsageKind == "forbidden"`)
- [x] 4.2 Add type-level, method-level, property-level, and field-level (including a private field) attribute match coverage, confirming no visibility filtering
- [x] 4.3 Add `attribute_prefixes` prefix-matching coverage
- [x] 4.4 Add coverage for a member decorated with two different configured attributes yielding two separate violations
- [x] 4.5 Add strict-failure vs audit-only-reporting tests for `attribute_usage`
- [x] 4.6 Add `ignored_violations` suppression + unmatched-ignore tracking test
- [x] 4.7 Add `CheckConfiguration_...` loader-rejection tests: empty `attributes`+`attribute_prefixes` is rejected; no `allowed_only_in_*`/`forbidden_in_*` is rejected
- [x] 4.8 Confirm full existing test suite still passes

## 5. Docs

- [x] 5.1 Add `docs/contracts/attribute-usage.md` (Groups, matching semantics, example YAML, When to use, Semantics, explicit Non-goals section: not runtime authorization/security validation, required-marker checks deferred to a documented follow-up)
- [x] 5.2 Update `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`
- [x] 5.3 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`
- [x] 5.4 Update `archlinternet.capabilities.json` with the new contract family entry

## 6. Validation

- [x] 6.1 Run `make fmt`
- [x] 6.2 Run `make acceptance`
- [x] 6.3 Fix any failures and rerun until green — decomposed `ArchitecturePolicyDocumentLoader.cs` (was pushed to 811 lines, over the 800-line hard limit) into `ArchitecturePolicyDocumentLoader.cs` (501 lines) + new `ArchitecturePolicyDocumentLoader.CoverageValidation.cs` (314 lines, partial class) to satisfy `lint-code-size`. The remaining 5 `ArchLinterNet.Core.Tests` failures (`ArchitectureProjectDiscoveryServiceFakeFileSystemTests` x2, `ArchitectureRepositoryRootResolverTests` x2, `ArchitectureSourceScannerFakeSeamTests` x1) and `make lint-docs`'s `python3` spawn error are pre-existing Windows-environment issues, confirmed identical on a clean `main`/pre-change checkout via `git stash -u`. All other suites are green: `ArchLinterNet.Cli.Tests` 80/80, `ArchLinterNet.Unity.Tests` 3/3, all 17 new `AttributeUsageContractTests` pass (777/782 in `ArchLinterNet.Core.Tests`), full solution builds clean, `dotnet format --verify-no-changes` clean, self-policy `dotnet run ... --mode strict` passes with full coverage.
