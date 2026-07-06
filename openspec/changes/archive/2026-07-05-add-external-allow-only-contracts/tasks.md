## 1. Contract model and schema

- [x] 1.1 Add `ArchitectureExternalAllowOnlyContract` to `ArchitectureContractModels.cs` (`Name`/`Id`/`Source`/`Allowed`/`AllowedTypes`/`IgnoredViolations`/`Reason`)
- [x] 1.2 Add `StrictExternalAllowOnly`/`AuditExternalAllowOnly` lists to `ArchitectureContractGroups`, wire into `EnumerateStrict`/`EnumerateAudit`
- [x] 1.3 Add `externalAllowOnlyContract` def and `strict_external_allow_only`/`audit_external_allow_only` array properties to `schema/dependencies.arch.schema.json`

## 2. Execution wiring

- [x] 2.1 Implement `CheckExternalAllowOnlyContract` in `ArchitectureAnalysisSession.Checking.cs`: iterate declared `Document.ExternalDependencies` groups not in `contract.Allowed` (sorted deterministically), reuse `ArchitectureExternalDependencyViolationFinder.FindViolations` per group, filter `AllowedTypes` out of the resulting violations' `ForbiddenReferences`, drop emptied violations
- [x] 2.2 Add `ExternalAllowOnlyContractHandler` (family `external_allow_only`) to `ArchitectureContractHandlers.cs`
- [x] 2.3 Register the new handler in `ServiceCollectionExtensions.cs`
- [x] 2.4 Add two new `AddGroup` calls (`strict_external_allow_only`, `audit_external_allow_only`, family `external_allow_only`) to `ArchitectureContractCatalog.Build`
- [x] 2.5 Add the two new groups to `ArchitecturePolicyDocumentLoader`'s duplicate-ID validation list

## 3. Tests

- [x] 3.1 Create `ExternalAllowOnlyContractTests.cs` (and fixtures if needed) covering: no external references, allowed-group reference passes, reference to non-allowed declared group violates, multiple disallowed groups each reported, `allowed_types` exception suppresses a specific reference, `ignored_violations` suppress a matching violation and unmatched entries are tracked
- [x] 3.2 Add explicit BCL/system reference tests: BCL reference not flagged when no matching group is declared; BCL reference flagged when explicitly captured by a declared non-allowed group; misspelled `allowed` group name has no relaxing effect
- [x] 3.3 Add an audit-mode test confirming `audit_external_allow_only` violations are reported without failing strict validation
- [x] 3.4 Confirm existing `ExternalDependencyContractTests.cs` and `AllowOnlyWithSuffixTests.cs` (and their full suites) still pass unchanged

## 4. Docs

- [x] 4.1 Add `docs/contracts/external-allow-only.md` (example, semantics, BCL behavior, scope note on method-body IL scanning being out of scope for this release)
- [x] 4.2 Update `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`
- [x] 4.3 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`

## 5. Validation

- [x] 5.1 Run `make fmt`
- [x] 5.2 Run `make acceptance`
- [x] 5.3 Fix any failures and rerun until green — fixed `ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder` (caused by this change). 5 other `ArchLinterNet.Core.Tests` failures (`ArchitectureProjectDiscoveryServiceFakeFileSystemTests` x2, `ArchitectureRepositoryRootResolverTests` x2, `ArchitectureSourceScannerFakeSeamTests` x1) and `make lint-docs`'s `python3` spawn error are pre-existing environment issues on this Windows machine, confirmed identical on a clean `main` checkout via `git stash` — unrelated to this change. Verified independently: `dotnet test ArchLinterNet.slnx` (same 5 failures, all other projects green incl. `ArchLinterNet.Cli.Tests` 63/63), `dotnet format --verify-no-changes` clean, `mkdocs build --strict` clean, self-policy `dotnet run ... --mode strict` passes.
