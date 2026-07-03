## Why

CLI, the public `ArchitectureValidator` API, and the Testing adapter call the `ArchitectureValidationService`/`ArchitectureBaselineService` static facades instead of the composed `ArchitectureEngine` introduced by `core-composition-root`, and `ArchLinterNet.Unity.AsmdefValidator` bypasses the shared seam entirely by directly instantiating the policy loader, repository-root resolver, and asmdef scanner. Issue #140 (child of #132) requires adapters to consume Core application services through the composition root, and requires Unity specifically to gain a narrow composed asmdef application service rather than reaching into loader/resolver/scanner internals.

## What Changes

- CLI (`Program.cs`) builds one `ArchitectureEngine` via `ArchitectureEngineBuilder().AddArchLinterNetCore()` and calls `engine.Validate(...)`/`engine.GenerateBaseline(...)` instead of the static facades.
- Public `ArchitectureValidator` and the Testing adapter's `ArchitectureValidationBuilder` each hold their own lazily-built `ArchitectureEngine` and call `engine.Validate(...)` instead of `ArchitectureValidationService.Validate(...)`.
- New narrow Core application service `ArchLinterNet.Core.Asmdef.IAsmdefValidationService`/`AsmdefValidationService`, registered in `AddArchLinterNetCore()`, wrapping policy load → repository-root resolution → `strict_asmdef` contract scanning.
- `ArchitectureEngine` gains `ValidateAsmdef(AsmdefValidationRequest)`, resolving `IAsmdefValidationService`.
- `ArchLinterNet.Unity.AsmdefValidator` becomes a thin facade over `ArchitectureEngine.ValidateAsmdef(...)`; it no longer references `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Resolution`, or `ArchLinterNet.Core.Scanning` directly.
- `IArchitectureAsmdefScanner`/`ArchitectureAsmdefScanner` become `public` (from `internal`) so the new public `AsmdefValidationService` constructor can take it as a dependency.
- Self-architecture policy (`architecture/dependencies.arch.yml`): add a `core_asmdef` layer, a `unity-must-use-asmdef-application-seam` strict contract mirroring the existing CLI seam rule, and a matching DI-container-forbidden rule for `core_asmdef`.
- The pre-existing `ArchitectureValidationService`/`ArchitectureBaselineService` static facades are unchanged and keep working for any external callers; adapters simply stop being the ones who call them.

## Capabilities

### New Capabilities
- `asmdef-validation-service`: the narrow Core application service that Unity (and any other narrow asmdef-only caller) consumes instead of contract loaders, repository-root resolution, and asmdef scanning directly.

### Modified Capabilities
- `shared-validation-service`: CLI, the public API, and the Testing adapter now call the composed `ArchitectureEngine` rather than the static `ArchitectureValidationService` facade; observable validation behavior is unchanged.
- `core-composition-root`: `ArchitectureEngine` gains a third operation, `ValidateAsmdef`, alongside `Validate`/`GenerateBaseline`.

## Impact

- `src/ArchLinterNet.Core/Asmdef/**` (new): `AsmdefValidationRequest`, `AsmdefValidationOutcome`, `Abstractions/IAsmdefValidationService`, `AsmdefValidationService`.
- `src/ArchLinterNet.Core/Composition/ArchitectureEngine.cs`, `ServiceCollectionExtensions.cs`: new `ValidateAsmdef` method and DI registration.
- `src/ArchLinterNet.Core/Scanning/ArchitectureAsmdefScanner.cs`: interface/class accessibility `internal` → `public`.
- `src/ArchLinterNet.Cli/Program.cs`, `src/ArchLinterNet.Core/ArchitectureValidator.cs`, `src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs`, `src/ArchLinterNet.Unity/AsmdefValidator.cs`: switch call sites to the composed engine.
- `architecture/dependencies.arch.yml`: new layer and strict/strict_external contracts.
- `tests/ArchLinterNet.Core.Tests/LayerTemplateContractTests.cs`: exhaustive layer-template fixture updated for the new `ArchLinterNet.Core.Asmdef` sub-namespace.
