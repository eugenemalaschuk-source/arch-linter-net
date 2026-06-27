## Why

Diagnostic output (dependency violations, cycles, configuration problems, external-dependency violations, unmatched ignores) is currently represented mostly through a single `ArchitectureViolation` record that accumulates optional fields per checker kind, plus loosely-typed raw cycle path lists. Formatters must inspect which optional fields are populated to infer what kind of diagnostic they're rendering, coupling them to every checker's specific shape. As more diagnostic kinds are added, this record keeps growing instead of the model expressing kinds explicitly. (GitHub issue #76, parent story #69.)

## What Changes

- Add a typed `ArchitectureDiagnostic` model: an abstract base with a `Kind` discriminator and sealed per-kind subtypes (`DependencyDiagnostic`, `CycleDiagnostic`, `UnmatchedIgnoreDiagnostic`, `ConfigurationDiagnostic`, `ExternalDependencyDiagnostic`), each carrying only the fields relevant to that kind.
- Add an adapter (`ArchitectureDiagnosticMapper`) that converts existing legacy checker output (`ArchitectureViolation`, raw cycle path collections, `ArchitectureUnmatchedIgnoredViolation`) into the new `ArchitectureDiagnostic` model.
- Update `ArchitectureDiagnosticFormatter` to consume `ArchitectureDiagnostic` instances (via the adapter) instead of reading `ArchitectureViolation`'s optional fields directly, while producing byte-identical human-readable and JSON output.
- Existing checker result types (`ArchitectureViolation`, `ArchitectureUnmatchedIgnoredViolation`, cycle detector output) are unchanged — this is a formatting-layer/model-layer refactor, not a checker change.
- No new output formats, no source locations, no new contract families (explicit non-goals).

## Capabilities

### New Capabilities
- `diagnostics-model`: defines the typed diagnostic envelope (kinds, fields per kind) and the adapter contract that converts legacy checker results into it.

### Modified Capabilities
(none — `violation-reporting` output behavior is unchanged; only its internal input type changes, which is an implementation detail, not a spec-level behavior change)

## Impact

- `src/ArchLinterNet.Core/Model/`: new `ArchitectureDiagnostic` hierarchy.
- `src/ArchLinterNet.Core/Reporting/`: new `ArchitectureDiagnosticMapper`; `ArchitectureDiagnosticFormatter` updated to consume the new model.
- `tests/ArchLinterNet.Core.Tests/`: new tests for diagnostic conversion and formatter regression (human + JSON), existing `UnifiedJsonOutputTests.cs` must continue passing unchanged.
- No changes to `src/ArchLinterNet.Cli/Program.cs` call sites beyond what's needed to keep behavior identical.
