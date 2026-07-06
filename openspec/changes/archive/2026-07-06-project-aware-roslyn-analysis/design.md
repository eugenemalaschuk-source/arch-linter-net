## Context

`RoslynCompilationFactory` (`Core.IO`) builds a `CSharpCompilation` from whatever assemblies the analyzer process happens to have loaded (`TRUSTED_PLATFORM_ASSEMBLIES` + `AppDomain` reflection). `ArchitectureSourceScanner` (`Core.Scanning`) enumerates `*.cs` files under `analysis.source_roots` (or discovered project directories from issue #56) filtered only by namespace declaration — no bin/obj/generated exclusion exists today.

Project discovery (issue #56, already merged) parses `.sln`/`.csproj` via plain XML, without MSBuild evaluation, and exposes `ArchitectureDiscoveredProject` (`Path`, `AssemblyName`, `TargetFrameworks`, `PackageReferences`) through `Context.ProjectDiscovery`. It does not resolve actual reference assembly paths or the real MSBuild `Compile` item list.

This repo enforces its own layering via `architecture/dependencies.arch.yml`:
- `core_io` (Roslyn compilation, file system, env, assembly loading) must not depend on `core_discovery`, `core_scanning`, `core_execution`, `core_validation`, `core_resolution`, `core_contracts`, `core_reporting`, or `core_composition` (`core-io-stays-independent`).
- `core_discovery` must not depend on `core_execution`/`core_validation` (`core-discovery-must-not-depend-on-execution-or-validation`).
- `core_scanning` must not depend on `core_execution`/`core_validation` (`core-scanning-must-not-depend-on-execution-or-validation`).
- Only `core_execution` currently depends on both `Core.Discovery` and `Core.Scanning`, and glues them together by passing plain data (e.g. `sourceRoots: string[]`) rather than by `Core.Scanning` importing `Core.Discovery` types.

Any design here must keep that shape: `core_io` and `core_scanning` stay ignorant of `Core.Discovery`'s types: they gain new *primitive* parameters (paths/strings), and `core_execution` is the only place that resolves discovery data and decides what to pass down.

## Goals / Non-Goals

**Goals:**
- Resolve method-body Roslyn symbols using real project/package/framework references, not incidental AppDomain state.
- Keep `core_io`/`core_scanning` decoupled from `Core.Discovery` (no new namespace dependency edges beyond what `core_execution` already has).
- Make it explicit in diagnostics when a check ran project-aware vs. fallback.
- Exclude generated and build-output files from source scanning by default, in both the fallback and project-aware paths.
- Preserve `analysis.condition_sets` behavior exactly as-is.
- Preserve current behavior byte-for-byte for repositories that do not configure `analysis.solution`/`analysis.projects`.

**Non-Goals:**
- Invoking `dotnet build`/`dotnet restore` automatically. Buildalyzer's design-time build only reads existing restore/build state (`obj/project.assets.json`, etc.); if that's missing, resolution fails and the fallback path + diagnostic kick in.
- Runtime/DI behavior resolution or semantic data-flow analysis (explicit non-goals in issue #61).
- Performance optimization, caching, or parallelism across the Buildalyzer evaluation (tracked separately under issue #19).
- Changing how `analysis.condition_sets` symbols are resolved or merged with MSBuild's own `DefineConstants` — condition-set symbols continue to be the literal, complete symbol list passed to `CSharpParseOptions`.
- Changing `IArchitectureProjectDiscoveryService`'s existing XML-based parsing (assembly name/target framework/package reference discovery) — this change adds a sibling resolver, it does not replace or restructure discovery's existing requirements.

## Decisions

### 1. New resolver lives in `Core.Discovery`, not `Core.IO`

A new internal interface, `IArchitectureProjectRoslynContextResolver`, is added to `Core.Discovery`. Given a repository root and an `ArchitectureDiscoveredProject`, it runs an MSBuild design-time build (via Buildalyzer's `AnalyzerManager`/`IProjectAnalyzer`, requesting a design-time build so the compiler itself is never invoked) and returns an `ArchitectureProjectRoslynContext` record:

```csharp
internal sealed record ArchitectureProjectRoslynContext(
    string ProjectPath,
    IReadOnlyList<string> SourceFilePaths,
    IReadOnlyList<string> ReferenceAssemblyPaths);
```

or, on failure, a typed failure reason consumed by `core_execution` to build a Configuration diagnostic. This keeps all Buildalyzer/`Microsoft.Build` usage inside `Core.Discovery`, alongside the existing solution/project-file parsers — it is a project-discovery concern (what does MSBuild say this project's compiler inputs actually are), not an IO or scanning concern.

**Alternative considered:** putting Buildalyzer logic directly in `Core.Scanning` (since that's where compilations are triggered). Rejected: `Core.Scanning` would then need to reference `Core.Discovery` types (`ArchitectureDiscoveredProject`) to know which project to evaluate, which is fine directionally, but mixing "how do I run MSBuild" into the scanning layer blurs the existing seam where scanning only knows about namespaces/patterns/files, not projects. Keeping MSBuild evaluation next to the existing project-file parsers is a smaller, more consistent surface.

### 2. `core_scanning`/`core_io` gain primitive parameters, not new dependencies

`IRoslynCompilationFactory.Create` gains an optional `IReadOnlyList<string>? explicitReferenceAssemblyPaths` parameter. When provided (non-empty), it is used *instead of* the `TRUSTED_PLATFORM_ASSEMBLIES`/`AppDomain` reflection path. `ArchitectureSourceScanner.FindMethodBodyViolations` does not need a matching source-file override parameter: per decision 3 below, the existing namespace-filtered file list is still what gets scanned — only which *references* the resulting compilation sees changes. The new parameter is a plain `IReadOnlyList<string>` — no new `using ArchLinterNet.Core.Discovery;` in either file. This preserves `core-io-stays-independent` and keeps `core_scanning`'s existing dependency surface unchanged.

**Alternative considered:** passing the whole `ArchitectureProjectRoslynContext` or `ArchitectureDiscoveredProject` object down into `Core.Scanning`/`Core.IO`. Rejected: would create a new `Core.Scanning -> Core.Discovery` and `Core.IO -> Core.Discovery` dependency edge, which the self-policy's own coverage/protected contracts would then need to special-case, and isn't necessary — the data these layers need is just lists of paths.

### 3. `core_execution` maps matched files to an owning project by directory containment

`ArchitectureAnalysisSession.CheckMethodBodyContract` already computes the list of matched source files (via the existing namespace-filtered enumeration, unchanged) before building a compilation. To attempt project-aware resolution, it groups those files by the nearest `ArchitectureDiscoveredProject` whose directory (derived from `Path`) is an ancestor of the file. This does not depend on any naming convention between assembly name and namespace (real repos may not follow one) and reuses data already available on `Context.ProjectDiscovery`.

If all matched files resolve to exactly one owning project, and `IArchitectureProjectRoslynContextResolver` succeeds for it, its resolved reference paths are passed to the compilation factory (source files stay as the existing namespace-matched list — we still only need semantic models for those files, not the whole project — but each is checked against the shared generated-file filter first). If matched files span multiple owning projects, or no owning project is found, or resolution fails, the check falls back to today's behavior for that contract, and a diagnostic is recorded when `Context.ProjectDiscovery` was configured (non-null) so the failure is visible instead of silently degrading accuracy.

**Alternative considered:** using `ArchitectureProjectRoslynContext.SourceFilePaths` (MSBuild's real Compile-item list) as the compilation's syntax trees instead of the existing namespace-filtered list. Rejected for this change: it would widen the compilation's syntax-tree set beyond what's needed for method-body scanning (which only inspects bodies in files declaring the target namespace) and risks behavior drift in violation counts unrelated to this issue's goal (reference accuracy). The MSBuild source list is still used to validate/cross-check generated-file exclusion, not to replace file selection.

### 4. Fallback diagnostic is embedded in the contract's own violation list, not routed through `Context.DiscoveryDiagnostics`

`Context.DiscoveryDiagnostics` is fixed at `ArchitectureAnalysisContext` construction time, before any contract check runs — but whether project-aware resolution succeeds is only known *during* a specific method-body contract's check. Threading a dynamically-discovered diagnostic back into the already-computed `CheckConfiguration()` output would require new session-level mutable state and careful ordering against `ArchitectureContractExecutor`'s existing execution sequence, for no real benefit. Instead, `CheckMethodBodyContract` appends the fallback notice directly to the `ArchitectureViolation` list it already returns for that contract — same shape used elsewhere for diagnostic-like entries (`ContractName`/`ContractId` from the contract, `ForbiddenNamespace` set to the kind string `"project-aware analysis fallback"`, `ForbiddenReferences` holding the human-readable reason). This keeps the diagnostic attributed to the specific contract that degraded, visible in the same violation list, without adding a new cross-cutting collection to the session.

### 5. Shared generated-file exclusion filter lives in `Core.Scanning`

A small helper (`ArchitectureGeneratedFileFilter.IsExcluded(string relativePath)`) excludes paths containing a `bin`, `obj`, `Library`, `Temp`, or `PackageCache` directory segment, or whose filename ends in `.g.cs`, `.g.i.cs`, or `.designer.cs` (case-insensitive). It is applied in `ArchitectureSourceScanner.FindSourceFilesForNamespace`, checked against the path *relative to the scanned source root* (not the absolute path) — critical so an ancestor directory outside the repository (e.g. the OS temp directory a test fixture or CI checkout happens to live under, which itself contains a `Temp` segment) is never mistaken for a Unity `Temp/` build folder inside the repo.

### 6. Buildalyzer confined to `Core.Discovery` via the self-policy

`Buildalyzer` is added as a `PackageReference` on `ArchLinterNet.Core.csproj` (it is a single package with no separate `Microsoft.Build` package needed at compile time; Buildalyzer resolves the MSBuild toolchain at runtime via the SDK already on the host). `architecture/dependencies.arch.yml` gains:

```yaml
external_dependencies:
  msbuild_project_evaluation:
    namespace_prefixes:
      - Buildalyzer
      - Microsoft.Build
```

and `strict_external` contracts forbidding every layer except `core_discovery` from depending on it, mirroring the existing `dependency_injection_container` confinement pattern already in the same file. This also means the new resolver's public-facing interface (`IArchitectureProjectRoslynContextResolver`) must return plain records (no Buildalyzer types leak into its return type), so `core_execution` never needs a reference to Buildalyzer itself.

## Risks / Trade-offs

- **[Risk] Buildalyzer's design-time build can be slow (spawns an MSBuild process per project).** → Mitigation: only attempted when `analysis.solution`/`analysis.projects` is configured and only for the project(s) owning a method-body contract's matched files (not the whole solution); explicitly out of scope to cache/parallelize (issue #19).
- **[Risk] Design-time build requires prior restore; CI/dev environments that haven't restored will always fall back.** → Mitigation: this is the intended, explicit fallback trigger per the issue's acceptance criteria ("diagnostics identify whether project-aware or fallback analysis was used when ambiguity matters"); the diagnostic names the project so it's actionable.
- **[Risk] A `ProjectReference`'s resolved reference-assembly path only physically exists after the *referenced* project has actually been built — restoring alone is not enough (MSBuild reports the expected `obj/.../ref/*.dll` path from a design-time build, but design-time builds skip compilation).** → Mitigation: `RoslynCompilationFactory` already filters resolved reference paths to ones that exist on disk (`IArchitectureFileSystem.FileExists`), so a not-yet-built project reference is silently omitted from the compilation's reference set rather than throwing; the same "your dependencies need to be built" expectation the existing `IArchitectureProjectDiscoveryService` (issue #56) already enforces for assembly-output resolution.
- **[Risk] A namespace's types could span more than one project (e.g. partial classes across projects, unusual repo layouts).** → Mitigation: when matched files span multiple owning projects, fall back rather than guessing which project's references to use; this is a conservative, backward-compatible default.
- **[Risk] New external dependency surface (Buildalyzer + its own transitive MSBuild-related packages).** → Mitigation: confined to `core_discovery` via a new `strict_external` contract, consistent with how the DI container is confined today; self-policy tests (`self-architecture-policy`, `self-policy-rule-input-coverage`) will catch any leakage.

## Migration Plan

No data migration. This is additive and backward-compatible:
1. Add Buildalyzer package reference + self-policy external-dependency confinement.
2. Add `IArchitectureProjectRoslynContextResolver` + record types in `Core.Discovery`, registered in `AddArchLinterNetCore()`.
3. Add the shared generated-file filter in `Core.Scanning`; apply it to the existing fallback enumeration (this alone changes behavior slightly for all repos — generated/build-output files stop being scanned — call out in release notes).
4. Extend `RoslynCompilationFactory`/`ArchitectureSourceScanner` with the new optional parameters (default `null`, so existing callers/tests are unaffected).
5. Wire owning-project mapping + project-aware attempt + fallback + diagnostic into `ArchitectureAnalysisSession.CheckMethodBodyContract`.
6. Add multi-project fixture(s) and tests per the issue's acceptance criteria.

Rollback: revert the change; the new parameters are additive and optional, so no other contract family is touched.

## Open Questions

- None blocking. The generated-file exclusion is a small, intentional behavior change for all repos (not gated behind discovery configuration) — flagged above as a release-note item rather than an open question.
