## Context

`ArchitectureAnalysisSession` is the per-validation-run context that owns `TypeIndex` (reflection) and `RoleIndex` (reflection + classification config), both computed lazily on first access. Path-convention rules (#113) and layout contracts (#170) need facts that neither index provides: source file paths, folder structure, and accurate type kinds (including `record`). Those facts require correlating CLR reflection metadata with C# syntax analysis.

Existing source-scanning infrastructure (`ArchitectureSourceScanner`, `ArchitectureDeclaredTypeParser`-to-be-introduced) already uses `Microsoft.CodeAnalysis.CSharp` and the `IArchitectureFileSystem` seam. The session already accepts source roots via `Document.Analysis.SourceRoots`. No new dependencies are needed.

## Goals / Non-Goals

**Goals:**
- Add `ArchitectureSourceFileFactIndex` to the session following the exact same lazy, per-run, scoped pattern as `TypeIndex` and `RoleIndex`.
- Provide deterministic facts: assembly name, namespace, full CLR type name, simple type name, type kind, normalized relative source path, file name without extension, folder segments (repo-relative), namespace segments.
- Detect and report ambiguous types (partial class declared in multiple source files) deterministically.
- Keep build cost at zero when no rule accesses the index (`Lazy<T>` guard).
- Support the `IArchitectureFileSystem` seam for full testability with `FakeArchitectureFileSystem`.

**Non-Goals:**
- No policy schema changes.
- No CLI or reporting surface changes.
- No CEL/expression exposure of the index.
- No path-convention or layout-contract evaluation (that is #113 and #170 work).
- No public documentation until a user-visible rule consumes the index.
- No runtime assembly loading, DI container inspection, or arbitrary filesystem traversal from policy expressions.

## Decisions

### Decision 1: Reflection-first, assembly-aware source-enriched merge strategy

**Rationale**: The public lookup API starts from CLR identity because callers hold `System.Reflection.Type` objects. Starting from reflection ensures every loadable type gets a fact even when source data is unavailable (e.g., generated assemblies, NuGet references). Source scanning then enriches matching facts with path data and Roslyn-accurate type kinds, but only after first resolving which source roots belong to which target assembly. Internally, source correlation is keyed by `(AssemblyName, FullTypeName)` rather than by `FullTypeName` alone, so a file from a non-target or unowned project cannot become source evidence for a target assembly by name collision alone.

**Alternative considered**: Source-first (parse all files, match to reflection). Rejected because the mapping from Roslyn-parsed name to CLR FullName has edge cases (anonymous types, compiler-generated types, types in global namespace) that are easier to handle starting from the authoritative CLR name.

### Decision 2: Syntax-only Roslyn parsing (no compilation)

**Rationale**: `CSharpSyntaxTree.ParseText()` requires no reference assemblies, no project context, and no MSBuild. It extracts namespace and type declarations from source text in milliseconds per file. A full compilation (`CSharpCompilation.Create(...)`) is only needed when resolving symbol references — unnecessary here since we only need syntactic declarations.

**Alternative considered**: Reuse `ArchitectureProjectRoslynContextResolver` (Buildalyzer). Rejected: too expensive for a static-facts pass; Buildalyzer runs a design-time MSBuild build per project, adding seconds of overhead.

### Decision 3: CLR-format full names in the parser output

**Rationale**: `Type.FullName` uses dots for namespace separators, `+` for nesting, and `` `N `` for generic arity (e.g., `MyApp.Outer+Repository`1`). The parser must produce the same format so index keys are directly comparable to reflection-side keys without transformation at lookup time.

**Alternative considered**: Roslyn-style dot-only names with generic syntax (`Repository<T>`). Rejected: would require a format conversion step on every `TryGetFact` lookup — fragile and slower.

### Decision 4: Folder segments relative to repository root

**Rationale**: All path facts in this codebase are normalized relative to `Context.RepositoryRoot`. Path-convention rules need the full path context (e.g., to match `"*/Domain/*"` patterns). Project-relative segments would require knowing which project owns each file, adding coupling to `ProjectDiscovery`.

**Alternative considered**: Project-relative folder segments. Rejected: requires per-file project ownership resolution; complex when files sit at project root; inconsistent with existing path normalization.

### Decision 5: Unowned or absent source roots → reflection-only facts, except standalone single-target roots

**Rationale**: When `Document.Analysis.SourceRoots` is empty, or when a scanned file cannot be tied to exactly one target assembly, the index still returns useful facts from reflection: assembly, namespace, type name, and reflection-derived type kind. Callers that need source paths will get `null` and can decide how to handle it. Producing an error would break all existing policies that do not use source facts, while guessing ownership would publish incorrect source evidence. Ownership is therefore resolved at the most specific known project subtree that contains each file, not at the coarse `source_root` level, so a shared configured root like `src` can still enrich files under `src/App` and `src/Domain` independently. The overlap check is symmetric: a configured root may be broader than a project directory (`src` covering `src/App`) or narrower than it (`src/App/Domain` inside project `src/App`), and a root-level project (`.`) owns files anywhere beneath repository-relative configured roots. The one safe standalone fallback is the single-target case: when the index is built directly with exactly one target assembly and explicit source roots, all configured roots are owned by that sole assembly, preserving the documented standalone `target_assemblies` + `source_roots` workflow.

### Decision 6: Ambiguity is tracked per owned assembly/type pair

**Rationale**: Partial classes spread across files make it impossible to deterministically assign one canonical source file to a type. Any multi-file occurrence for the same owned `(AssemblyName, FullTypeName)` pair is recorded as `ArchitectureDeclaredTypeSourceAmbiguity`; the corresponding fact gets `null` for `SourceFilePath`. This is conservative but correct — a rule author can read the ambiguity list and decide how to handle it.

### Decision 7: Effective preprocessor symbols participate in source correlation

**Rationale**: Source enrichment must match the declarations that were actually compiled for the current validation run. The session's effective preprocessor symbols are therefore forwarded into Roslyn syntax parsing, so declarations behind inactive `#if` branches do not become false source evidence.

### Decision 8: Full-name lookup is conservative when CLR names collide

**Rationale**: `TryGetFact(fullTypeName)` is a convenience API only for unique CLR names. When two target assemblies declare the same CLR full name, the single-argument overload returns `false` rather than silently choosing one assembly, while `TryGetFact(assemblyName, fullTypeName)` remains the exact disambiguating API.

### Decision 9: Record detection requires source

**Rationale**: `System.Reflection.Type` has no `IsRecord` property. Records in CLR metadata are classes or structs with compiler-generated members (no reliable reflection-only signal). Roslyn syntax analysis cleanly identifies `RecordDeclarationSyntax`. When source is unavailable, the type kind falls back to `Class` or `Struct`. This is documented as a known limitation; the issue says "where supported".

## Risks / Trade-offs

- **Partial-class over-ambiguity**: A type with 10 partial files across a large codebase will appear as ambiguous. Path-convention rules cannot evaluate it. This is correct behavior, but authors must be aware. → Mitigation: `Ambiguities` list gives full visibility; no silent suppression.
- **CLR-name format edge cases**: Anonymous types (`<>c__DisplayClass`), compiler-generated state machines, and global-namespace types have unusual `FullName` values. They will appear in `AllFacts` with reflection-derived data but typically have no matching source file. → Mitigation: source-absent facts are valid (null `SourceFilePath`); path rules simply won't apply to them.
- **Source root drift / ownership gaps**: If project discovery does not run (e.g., assembly-only mode), or a root spans code whose owning target assembly cannot be determined, source data is unavailable for those types. → Mitigation: this is expected and documented behavior; reflection-only facts are still returned instead of guessing.
- **Performance on very large codebases**: Syntax-parsing thousands of `.cs` files adds wall-clock time on first access. Lazy computation limits this to runs that actually invoke source-fact-dependent rules. → Mitigation: acceptable; no impact on runs that don't use source facts.

## Open Questions

None. All design decisions are resolved.
