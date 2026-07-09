## 1. Move `IArchitectureAsmdefScanner` into `Scanning.Abstractions`

- [x] 1.1 Create `src/ArchLinterNet.Core/Scanning/Abstractions/IArchitectureAsmdefScanner.cs` containing only the interface, namespace `ArchLinterNet.Core.Scanning.Abstractions`.
- [x] 1.2 Remove the interface declaration from `src/ArchLinterNet.Core/Scanning/ArchitectureAsmdefScanner.cs`, keeping the concrete `ArchitectureAsmdefScanner` class (namespace stays `ArchLinterNet.Core.Scanning`), and add `using ArchLinterNet.Core.Scanning.Abstractions;`.

## 2. Fix consumers

- [x] 2.1 Update `src/ArchLinterNet.Core/Asmdef/AsmdefValidationService.cs`'s `using` directives for the new interface namespace.
- [x] 2.2 Update `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` to add `using ArchLinterNet.Core.Scanning.Abstractions;` alongside the existing `using ArchLinterNet.Core.Scanning;`.

## 3. Docs

- [x] 3.1 Update `docs/internal/core-architecture-blueprint.md`'s "Core interface namespace convention" inventory table: move `IArchitectureAsmdefScanner` out of the internal-feature-seam row into a replaceable-infrastructure-seam row targeting `ArchLinterNet.Core.Scanning.Abstractions`.
- [x] 3.2 Reconcile the existing infrastructure-seams narrative sentence (around "Asmdef scanning") that already described `IArchitectureAsmdefScanner` as consumed by "the Unity `IAsmdefValidationService`" so it correctly attributes the consumer to Core's own `Asmdef.AsmdefValidationService`.
- [x] 3.3 Record, in the same table/section, that `IArchitectureDiagnosticFormatter`, `IArchitectureSarifFormatter`, `IArchitectureSourceScanner`, `IArchitectureIlMethodBodyScanner`, and `IArchitectureExternalDependencyIlScanner` were re-audited against the same rule and confirmed to remain internal feature seams (no folder change).

## 4. Verify

- [x] 4.1 Build the solution and confirm no missing/broken `using` directives.
- [x] 4.2 Run the full test suite / repository acceptance gate and confirm no behavior change (in particular `AsmdefValidationEngineTests`, `AsmdefValidatorTests`, `AsmdefValidatorDelegationTests`).
- [x] 4.3 Run the repository's own self-policy/architecture-coverage check against itself and confirm no new `unknown`/uncovered findings for `ArchLinterNet.Core.Scanning.Abstractions`.
