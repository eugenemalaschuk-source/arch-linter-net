## Why

`ArchitectureDiagnosticFormatter.NormalizedDetails.cs` contains one central `switch (diagnostic)` statement (`ApplyDiagnosticSpecificCiFields`) enumerating all 24 sealed `ArchitectureDiagnostic` subtypes to build each finding's structured CI/JSON detail fields. Every independent diagnostic-family feature episode in the v0.6.0 release chain (#303, #349, #385, #393, #395, #412) has had to extend this one switch, even though most families already own dedicated `Apply<Family>CiFields` methods in their own formatter partial files. This makes diagnostic-family growth a closed central modification point at the reporting layer, contrary to the open/closed intent already established elsewhere in the codebase (e.g. the contract-family registry, #211).

## What Changes

- Introduce `DiagnosticDetailProjectionRegistry`, a static, ordered registry keyed by each diagnostic's exact CLR `Type`, mapping all 24 sealed `ArchitectureDiagnostic` subtypes to a projector delegate that fills in that family's structured CI/JSON detail fields. Mirrors the existing `ArchitectureContractFamilyRegistry` / `ArchitectureContractHandlerRegistry` idiom already used in `src/ArchLinterNet.Core/Execution/`.
- Replace the switch body of `ApplyDiagnosticSpecificCiFields` with a registry lookup and invoke, throwing for an unregistered type (defense in depth, mirrors `ArchitectureContractHandlerRegistry.Execute`).
- Extract the diagnostic kinds whose projection logic was inlined directly in the switch (not yet delegating to a family-owned method) into their own named `Apply<Kind>CiFields` static methods, with an identical field-by-field body — same keys, same order, same conditionals — so wire output stays byte-identical.
- Move `ApplyBuildStatePreflightCiFields` into `ArchitectureDiagnosticFormatter.BuildStatePreflight.cs`, the family's existing dedicated partial, consistent with "family owns its own projection."
- Add a reflection-based completeness test enumerating every sealed non-abstract `ArchitectureDiagnostic` subtype in the Core assembly and asserting each has a registry entry, so a new diagnostic kind added without a registered projector fails a test instead of silently omitting structured output.
- Out of scope, unchanged: `BuildHumanContext`'s human-readable `if (diagnostic is X)` chain, the shared-field switches `SourceTypeOf`/`ForbiddenNamespaceOf`/`ForbiddenReferencesOf`, every switch in `ArchitectureFindingMapper.cs`, SARIF formatter architecture, the Testing adapter, and normalized-finding schema semantics. No new diagnostics or contract families are introduced. The registry is a static compiled list, not a runtime-discovered plugin mechanism; reflection is used only in the completeness test.

## Capabilities

### New Capabilities
- `diagnostic-detail-projection-registry`: the type-keyed, ordered static registry that each diagnostic family's structured CI/JSON detail projector is registered against, replacing the central all-kinds switch as the extension point for adding a new diagnostic family's structured output.

### Modified Capabilities
- `diagnostics-model`: the Requirement "Formatters consume the diagnostic model without checker-specific knowledge" currently states `ArchitectureDiagnosticFormatter` SHALL render CI JSON "by pattern-matching on `ArchitectureDiagnostic` kind." This is narrowed: CI/JSON structured detail projection now dispatches through the per-family registered projector in `diagnostic-detail-projection-registry`; pattern-matching remains accurate for the shared display fields and human-readable output, which are unchanged by this proposal.

## Impact

- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.NormalizedDetails.cs`: switch replaced by registry lookup; several extracted `Apply<Kind>CiFields` methods added; `ApplyBuildStatePreflightCiFields` moved out.
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.BuildStatePreflight.cs`: gains `ApplyBuildStatePreflightCiFields`.
- New file: the registry type under `src/ArchLinterNet.Core/Reporting/`.
- `tests/ArchLinterNet.Core.Tests/`: new completeness test; existing formatter tests must pass unmodified since wire output does not change.
- No changes to `src/ArchLinterNet.Core/Reporting/ArchitectureSarifFormatter*.cs`, `src/ArchLinterNet.Testing/**`, or `ArchitectureFindingMapper.cs` — all are unaffected downstream consumers or out-of-scope switches.
