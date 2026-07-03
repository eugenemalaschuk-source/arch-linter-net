## 1. Narrow Core asmdef application service

- [x] 1.1 Add `AsmdefValidationRequest`/`AsmdefValidationOutcome` and `IAsmdefValidationService` under `ArchLinterNet.Core.Asmdef`
- [x] 1.2 Implement `AsmdefValidationService` wrapping `IArchitecturePolicyDocumentLoader.Load`, `IArchitectureRepositoryRootResolver.ResolveFrom`, and `IArchitectureAsmdefScanner.FindAsmdefViolations` over `document.Contracts.StrictAsmdef`
- [x] 1.3 Make `IArchitectureAsmdefScanner`/`ArchitectureAsmdefScanner` `public` so the public `AsmdefValidationService` constructor can depend on it (required for MS.DI's public-constructor resolution)
- [x] 1.4 Register `IAsmdefValidationService` → `AsmdefValidationService` as a singleton in `AddArchLinterNetCore()`
- [x] 1.5 Add `ArchitectureEngine.ValidateAsmdef(AsmdefValidationRequest)` resolving `IAsmdefValidationService`

## 2. Rewire adapters onto the composed engine

- [x] 2.1 CLI `Program.cs`: build one `Lazy<ArchitectureEngine>`; replace `ArchitectureValidationService.Validate`/`ArchitectureBaselineService.Generate` calls with `engine.Validate`/`engine.GenerateBaseline`; update the version-string type reference
- [x] 2.2 Public `ArchitectureValidator`: hold a private lazily-built `ArchitectureEngine`; replace the static facade call with `engine.Validate`
- [x] 2.3 Testing `ArchitectureValidationBuilder`: hold a private lazily-built `ArchitectureEngine`; replace the static facade call with `engine.Validate`
- [x] 2.4 Unity `AsmdefValidator`: replace direct `new ArchitecturePolicyDocumentLoader()`/`new ArchitectureRepositoryRootResolver()`/`new ArchitectureAsmdefScanner()` with a call to `engine.ValidateAsmdef(...)`; remove the now-unused `Core.Contracts`/`Core.Resolution`/`Core.Scanning` usings

## 3. Self-architecture policy

- [x] 3.1 Add `core_asmdef` layer (`ArchLinterNet.Core.Asmdef`) to `architecture/dependencies.arch.yml`
- [x] 3.2 Add `unity-must-use-asmdef-application-seam` strict contract mirroring `cli-must-use-validation-application-seam`
- [x] 3.3 Add `core-asmdef-must-not-depend-on-di-container` strict_external contract mirroring the `core_validation` one
- [x] 3.4 Add `testing-must-use-validation-application-seam` strict contract mirroring `cli-must-use-validation-application-seam`, forbidding `ArchLinterNet.Testing` from depending on `core_execution`/`core_contracts`/`core_resolution`/`core_scanning`

## 4. Tests and validation

- [x] 4.1 Update `LayerTemplateContractTests.CheckLayerContract_Exhaustive_AllChildrenMapped_NoViolation` fixture to include the new `ArchLinterNet.Core.Asmdef` sub-namespace
- [x] 4.2 Confirm existing CLI/Core/Testing/Unity tests remain green
- [x] 4.3 Manually verify an asmdef editor-reference violation still surfaces through the rewired `AsmdefValidator`
- [x] 4.4 Add `AsmdefValidationEngineTests` (Core.Tests) proving `ArchitectureEngineBuilder().AddArchLinterNetCore().Build()` resolves and executes `ValidateAsmdef`, that `ValidateAsmdef` delegates to the registered `IAsmdefValidationService`, and that `audit_asmdef` contracts are never evaluated by the asmdef-only seam
- [x] 4.5 Add `AsmdefValidatorDelegationTests` (Unity.Tests) proving a failing `strict_asmdef` contract still produces the expected Core violation through the Unity-facing adapter, and that `audit_asmdef` contracts are not accidentally evaluated
- [x] 4.6 Run `make fmt`
- [x] 4.7 Run `make acceptance` (repo has no `Taskfile`/`task acceptance:fresh`; `make acceptance` is the equivalent target) and confirm it passes, including self-policy `dotnet arch-linter-net` strict/audit runs
- [x] 4.8 Run `openspec validate --all`
