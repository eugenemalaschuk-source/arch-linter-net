## Why

`ArchitectureViolation` has accumulated ~35 nullable fields, one cluster per diagnostic family (configuration/template, external dependency, package dependency, type placement, public API surface, attribute usage, inheritance, interface implementation, composition, project metadata, plus dependency's own layer fields). `ArchitectureDiagnosticMapper.FromViolation` infers which family a violation belongs to with a 10-branch if-chain that checks which nullable field is populated. Every new contract family has required adding more nullable fields to the shared record and another inference branch to the mapper, coupling all families to two central, ever-growing types. A newer family (`PolicyConsistencyDiagnostic`) already avoids this by being constructed directly as its own sealed record, bypassing `ArchitectureViolation` and the mapper entirely — this change generalizes that pattern to the remaining families so the next family follows the same recipe. (GitHub issue #214, parent story #183, follows #213.)

## What Changes

- Add an `IArchitectureDiagnosticPayload` abstraction (`ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation)`) that a family-owned payload type implements to build its own `ArchitectureDiagnostic` subtype.
- Collapse `ArchitectureViolation`'s ~35 family-specific nullable fields into a single `IArchitectureDiagnosticPayload? Payload { get; init; }` property. `MatchedNamespacePrefixes` stays on `ArchitectureViolation` directly since it is genuinely cross-family shared metadata (already documented as available on any diagnostic kind).
- Add one sealed payload record per family (Configuration, ExternalDependency, PackageDependency, TypePlacement, PublicApiSurface, AttributeUsage, Inheritance, InterfaceImplementation, Composition, ProjectMetadata, and Dependency itself for its `SourceLayer`/`TargetLayer`/`AllowedImporters` fields), each carrying only that family's fields.
- Replace `ArchitectureDiagnosticMapper.FromViolation`'s if-chain with `violation.Payload?.ToDiagnostic(violation) ?? new DependencyDiagnostic(...)` — a null check plus one dispatch call, not a type-inference cascade. Adding a future family means adding a new payload type and construction call; it requires no edits to `ArchitectureViolation` or the mapper.
- Update the ~14 production call sites that currently set family-specific bag fields on `ArchitectureViolation` to construct the corresponding payload instead. Call sites that only ever produce plain dependency violations (e.g. `AssemblyIndependenceChecker`) are unchanged.
- Preserve existing JSON, SARIF, and human-readable output exactly — this is an internal representation refactor, not a behavior or format change. `ArchitectureDiagnostic` subtypes, `ArchitectureDiagnosticFormatter`, and `ArchitectureSarifFormatter` (which already consume the diagnostic-side model, not the violation bag) are unaffected.
- Add regression tests proving output parity across several families before/after the refactor.
- Document the new "add a family" recipe (payload type + construction call, no shared-type edits) in `docs/internal/core-architecture-blueprint.md`.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `diagnostics-model`: the adapter requirement changes from "mapper infers diagnostic kind from `ArchitectureViolation`'s populated nullable fields" to "mapper dispatches to a payload-owned `ToDiagnostic` method"; add a requirement that family-specific evidence is carried on a payload type rather than as fields on the shared `ArchitectureViolation` record, with output compatibility preserved.

## Impact

- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`: replace ~35 nullable fields with `MatchedNamespacePrefixes` + `Payload`.
- `src/ArchLinterNet.Core/Model/`: new `IArchitectureDiagnosticPayload` interface and one payload record per family.
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`: `FromViolation` becomes payload-dispatch instead of an if-chain.
- ~14 production files across `src/ArchLinterNet.Core/Execution/` and `src/ArchLinterNet.Core/Scanning/` that currently set family-specific bag fields on `ArchitectureViolation` (see design.md for the full list), updated to construct payloads instead.
- `tests/ArchLinterNet.Core.Tests/`: update tests that reference the removed bag fields (`ArchitectureDiagnosticMapperTests.cs`, `ArchitectureSarifFormatterTests.cs`, `ProtectedContractTests.EdgeCases.cs`, others as found); add output-parity regression tests.
- `docs/internal/core-architecture-blueprint.md`: document the new family-addition recipe.
- No changes to `ArchitectureDiagnostic` subtypes, `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`, CLI commands, or the Testing API surface — all consume the diagnostic-side model, which is unchanged.
