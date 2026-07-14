## Why

Semantic role discovery and dependency contracts can work with roles and namespaces, but path-convention rules (#113) and layout contracts (#170) need static facts about source files and declared types that are not derivable from assembly metadata alone. This change adds a deterministic, per-run index of those facts as a foundational capability.

## What Changes

- **New**: `ArchitectureTypeKind` enum (Class, Interface, Struct, Enum, Record, Delegate, Unknown) in the model layer.
- **New**: `ArchitectureDeclaredTypeFact` record — the canonical fact unit exposing assembly name, namespace, full and simple type name, type kind, normalized source file path, file name, folder segments, and namespace segments.
- **New**: `ArchitectureDeclaredTypeSourceAmbiguity` record — signals when a type is declared across multiple source files (partial classes), making path facts non-deterministic for that type.
- **New**: `ArchitectureDeclaredTypeParser` (internal) — Roslyn syntax-only parser (no compilation) that extracts declared types from C# source text and produces CLR-format full names.
- **New**: `ArchitectureSourceFileFactIndex` — per-run, lazily-computed index. Builds from reflection (assembly/namespace/name/kind) enriched by source file scanning (path, folder segments, Roslyn-accurate type kind). Exposes lookup by full type name, file path, and namespace.
- **New**: `ArchitectureAnalysisSession.SourceFileFactIndex` property — wired alongside the existing `TypeIndex` and `RoleIndex`, following the same lazy, scoped pattern.

## Capabilities

### New Capabilities

- `source-file-fact-index`: Per-run index of declared-type facts — assembly name, namespace, type name, type kind, source file path, folder segments, namespace segments — with deterministic ambiguity diagnostics for partial-class multi-file declarations.

### Modified Capabilities

- `analysis-session-indexes`: The session gains a third index (`SourceFileFactIndex`) alongside `TypeIndex` and `RoleIndex`, following identical lazy-on-first-access, one-session-per-run semantics.

## Impact

- `src/ArchLinterNet.Core/Model/` — two new public record types and one new enum.
- `src/ArchLinterNet.Core/Scanning/` — one new internal static class (`ArchitectureDeclaredTypeParser`).
- `src/ArchLinterNet.Core/Execution/` — one new public class (`ArchitectureSourceFileFactIndex`) and one new property on `ArchitectureAnalysisSession`.
- `tests/ArchLinterNet.Core.Tests/` — new test file covering all source-file-fact scenarios.
- No policy schema changes. No CLI surface changes. No breaking changes to existing public API. No performance impact on existing rules that do not access the new index.
