## Why

Attribute-based role extraction (#109) computes each type's role, metadata, and classification source, but discards that result immediately after harvesting conflict/failure facts — nothing keeps a queryable, per-type role descriptor for a run, and `CheckClassificationFacts()` re-runs the full extraction pass on every call. Selectors and coverage checks (tracked separately, #106) need a stable way to ask "what role does this type have, and why" without recomputing extraction or re-deriving explainability from scratch.

## What Changes

- Add `ArchitectureRoleIndex`, a per-run index exposed from `ArchitectureAnalysisSession` (alongside `TypeIndex`/`ReferenceGraph`), lazily computing and caching each type's resolved role descriptor (role, metadata, classification source, evidence) in a single extraction pass.
- Add lookup APIs on the index: resolve a type's role descriptor, and enumerate all classified types — usable by future selectors/coverage checks.
- Add an explainable role-descriptor shape that reports which classification source won (`type_attribute`, `assembly_attribute`, or a not-yet-implemented source) and the evidence backing it, reusing the existing `ArchitectureClassificationSource` enum's role rather than inventing a parallel one.
- Refactor `ArchitectureAnalysisSession.CheckClassificationFacts()` to read conflicts/metadata failures from the new index's single cached pass instead of re-running `ArchitectureAttributeRoleExtractor` on every call.
- Add a JSON output shape for discovered role descriptors (e.g. `classification_roles`) to CI-artifact output, following the existing `classification_conflicts`/`classification_metadata_failures` convention.
- Policies without a `classification` section continue to produce an empty index at negligible cost (no role descriptors, no diagnostics), matching current no-op behavior.

## Capabilities

### New Capabilities
- `semantic-role-index`: per-run, lazily-computed index of type role descriptors (role, metadata, source, evidence) with lookup APIs and explainable diagnostics, scoped to one validation run.

### Modified Capabilities
- `analysis-session-indexes`: `ArchitectureAnalysisSession` gains a third lazily-scoped index (`RoleIndex`) alongside `TypeIndex`/`ReferenceGraph`, following the same one-session-per-run, cached-on-first-access pattern.

Note: `CheckClassificationFacts()` is refactored to read from the new index's cached pass instead of re-invoking `ArchitectureAttributeRoleExtractor` per call, but its signature, return shape, and reported values are unchanged — this is an implementation detail of `attribute-role-extraction`, not a spec-level behavior change, so no delta is filed against that capability.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs` and `.Classification.cs` — new `RoleIndex` property, refactored `CheckClassificationFacts()`.
- New file `src/ArchLinterNet.Core/Execution/ArchitectureRoleIndex.cs` — the index itself.
- New/updated model in `src/ArchLinterNet.Core/Model/` — role descriptor shape (or reuse of `ArchitectureTypeClassificationResult`).
- `src/ArchLinterNet.Core/Validation/ValidationOutcome.cs`, `ArchitectureValidationApplicationService.cs` — thread role descriptors through to outcome.
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs`, `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandHandler.cs`, `ICliRuntime`/`CliRuntime` — new JSON/human output section.
- `tests/ArchLinterNet.Core.Tests/` and `tests/ArchLinterNet.Cli.Tests/` — new index tests, refactored classification tests, formatter/CLI coverage.
