## Why

Issue #201 (child of #183) asks Reporting and Scanning to follow the same interface-namespace convention the rest of `ArchLinterNet.Core` already follows: a bounded `*.Abstractions` namespace for any interface that crosses an internal module boundary, in a file separate from its implementation. The prior audit (`2026-07-03-normalize-core-abstractions`) deliberately left every Reporting/Scanning interface in place because none of them crossed a module boundary at the time. Re-auditing today shows one has since started to: `IArchitectureAsmdefScanner` is now consumed by `ArchLinterNet.Core.Asmdef.AsmdefValidationService`, a different Core module, so it now meets the existing "replaceable infrastructure seam" bar the rest of Core already uses.

## What Changes

- Split `src/ArchLinterNet.Core/Scanning/ArchitectureAsmdefScanner.cs` into an interface-only file `Scanning/Abstractions/IArchitectureAsmdefScanner.cs` (namespace `ArchLinterNet.Core.Scanning.Abstractions`) and leave the concrete `ArchitectureAsmdefScanner` class in `Scanning/ArchitectureAsmdefScanner.cs` (namespace `ArchLinterNet.Core.Scanning`), matching the split-file pattern already used for every other moved interface.
- Fix the `using` directives that reference `IArchitectureAsmdefScanner`: `Asmdef/AsmdefValidationService.cs` and `Composition/ServiceCollectionExtensions.cs`.
- Re-audit the other 5 Reporting/Scanning interfaces (`IArchitectureDiagnosticFormatter`, `IArchitectureSarifFormatter`, `IArchitectureSourceScanner`, `IArchitectureIlMethodBodyScanner`, `IArchitectureExternalDependencyIlScanner`) against the same rule; none is consumed outside its own folder plus `Composition`, so all 5 stay where they are — recorded as a confirmed re-audit, not an oversight.
- Audit other first-party areas for the same missing-`Abstractions` pattern per #201's ask (`Model`, `Composition`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, `ArchLinterNet.Unity`): none define any interface, so there is nothing else to move and no follow-up to split out.
- Update `docs/internal/core-architecture-blueprint.md`'s "Core interface namespace convention" inventory table to move `IArchitectureAsmdefScanner` out of the "Internal feature seam" row into a "Replaceable infrastructure seam" row targeting `ArchLinterNet.Core.Scanning.Abstractions`, and reconcile the existing sentence that already (inconsistently) noted it being consumed by "the Unity `IAsmdefValidationService`" (that consumer is actually Core's own `Asmdef.AsmdefValidationService`, not a type in the `ArchLinterNet.Unity` assembly).

## Capabilities

### New Capabilities

(none — this is a namespace/file reorganization with no new capability)

### Modified Capabilities

- `asmdef-validation-service`: the "Narrow asmdef application service composes loader, resolver, and scanner" requirement now names its scanning collaborator's fully-qualified interface, `ArchLinterNet.Core.Scanning.Abstractions.IArchitectureAsmdefScanner`, reflecting the new namespace (this is a text/FQN update only — the requirement's observable behavior and scenarios are unchanged).

`openspec/specs/self-architecture-policy/spec.md` and `openspec/specs/core-composition-root/spec.md` state their constraints at the `ArchLinterNet.Core.Scanning` module-namespace-prefix level, not against the specific `IArchitectureAsmdefScanner` FQN, so no delta is needed there — `ArchLinterNet.Core.Scanning.Abstractions` remains `ArchLinterNet.Core.Scanning`-prefixed and stays subject to the same existing rules.

## Impact

- `src/ArchLinterNet.Core/Scanning/`: new `Abstractions/` subfolder holds the split-out `IArchitectureAsmdefScanner` interface file; `ArchitectureAsmdefScanner.cs` keeps the concrete class with an updated `using` directive.
- `src/ArchLinterNet.Core/Asmdef/AsmdefValidationService.cs`: `using` directive updated to `ArchLinterNet.Core.Scanning.Abstractions`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: new `using ArchLinterNet.Core.Scanning.Abstractions;` added alongside the existing `using ArchLinterNet.Core.Scanning;`.
- `docs/internal/core-architecture-blueprint.md`: inventory table and the Asmdef-scanning consumer sentence updated to match the new namespace and the corrected consumer description.
- No changes to `architecture/dependencies.arch.yml` (the existing `core_scanning` namespace-prefix layer already matches the new sub-namespace), CLI behavior, public API surfaces, or any test assertions beyond `using` directives (no test currently references `IArchitectureAsmdefScanner` by name).
