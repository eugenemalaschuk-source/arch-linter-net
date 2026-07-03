## 1. Hidden-global-state loaders/resolvers

- [ ] 1.1 Move `ArchitectureRepositoryRootLocator`'s logic into `ArchitectureRepositoryRootResolver`; remove `_root` `Lazy<string>`; delete the static class
- [ ] 1.2 Move `ArchitectureContractLoader`'s logic into `ArchitecturePolicyDocumentLoader`, injecting `IArchitectureRepositoryRootResolver` for the repo-root auto-discovery path; remove `_document` `Lazy<T>`; keep `NormalizeToContractId` as a `public static` method on the (now non-static) class; delete the static class
- [ ] 1.3 Update `AsmdefValidator.cs` (Unity) call sites for both classes
- [ ] 1.4 Update all test call sites for both classes
- [ ] 1.5 Add/update tests proving no cross-call caching regression (each call re-resolves against its own fake file system state)

## 2. Discovery and assembly resolution

- [ ] 2.1 Convert `ArchitectureSolutionParser` to `internal` instance class `ArchitectureSolutionParser : IArchitectureSolutionParser`
- [ ] 2.2 Convert `ArchitectureProjectFileParser` to `internal` instance class `ArchitectureProjectFileParser : IArchitectureProjectFileParser`
- [ ] 2.3 Move `ArchitectureProjectDiscovery`'s logic into `ArchitectureProjectDiscoveryService`, constructor-injecting the two parsers above; delete the static class
- [ ] 2.4 Move `ArchitectureAssemblyResolver`'s logic into `ArchitectureAssemblyResolutionService`; delete the static class
- [ ] 2.5 Update all test call sites for these four classes

## 3. Baseline and diagnostics services

- [ ] 3.1 Fold `ArchitectureBaselineLoader.LoadFromPath` and `ArchitectureBaselineMerger.Merge`/`MergeAndValidate` (with its nested `ContractGroupMerger` helper) into `ArchitectureBaselineLoadingService` as instance methods; delete both static classes
- [ ] 3.2 Create `IArchitectureBaselineGenerator`/`ArchitectureBaselineGenerator` instance class from the static `ArchitectureBaselineGenerator`; register `AddSingleton`; update `ArchitectureBaselineApplicationService` to take it via constructor injection
- [ ] 3.3 Create `IArchitectureDiagnosticFormatter`/`ArchitectureDiagnosticFormatter` instance class from the static formatter; register `AddSingleton`; update `Program.cs` (CLI) and `ArchitectureValidationResult.cs` (Testing) call sites
- [ ] 3.4 Update all test call sites for baseline loading/merging/generation and diagnostic formatting

## 4. Scanners

- [ ] 4.1 Create `IArchitectureAsmdefScanner`/`ArchitectureAsmdefScanner` instance class; register `AddSingleton`; update `ArchitectureAnalysisSession.Checking.cs` to construct its own instance; update `AsmdefValidator.cs` (Unity)
- [ ] 4.2 Create `IArchitectureSourceScanner`/`ArchitectureSourceScanner` instance class; register `AddSingleton`; update `ArchitectureAnalysisSession.Checking.cs`
- [ ] 4.3 Create `IArchitectureExternalDependencyIlScanner`/`ArchitectureExternalDependencyIlScanner` instance class; register `AddSingleton`; update `ArchitectureAnalysisSession.Checking.cs`
- [ ] 4.4 Create `IArchitectureIlMethodBodyScanner`/`ArchitectureIlMethodBodyScanner` instance class; register `AddSingleton`; update `ArchitectureAnalysisSession.Checking.cs`
- [ ] 4.5 Update all test call sites for the four scanners

## 5. Composition root and inventory

- [ ] 5.1 Add all new `AddSingleton` registrations to `ServiceCollectionExtensions.AddArchLinterNetCore()` in dependency order
- [ ] 5.2 Verify no type under `Execution`/`Scanning`/`Resolution`/`Discovery`/`Contracts` references `IServiceProvider` or container types (self-architecture policy should already enforce this)
- [ ] 5.3 Update `docs/internal/static-class-inventory.md`: move all 12 converted classes out of section (e) "follow-up candidate" into a "converted" status with the replacing interface named; note the `ArchitectureBaselineLoader`/`ArchitectureBaselineMerger` consolidation and the scanner direct-instantiation decision explicitly
- [ ] 5.4 Confirm guardrail bullets at the bottom of the inventory doc still hold (no new static production services, no new hidden `Lazy<T>`)

## 6. Validation

- [ ] 6.1 Run `make fmt`
- [ ] 6.2 Run `task acceptance:fresh` and fix any failures
- [ ] 6.3 Confirm existing tests remain green and new/updated tests cover the hidden-global-state removals and at least one scanner/parser/resolver conversion path
- [ ] 6.4 Run `openspec validate --all`
