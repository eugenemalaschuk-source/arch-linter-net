## Why

The Roslyn source scanner behind method-body contracts (`RoslynCompilationFactory` + `ArchitectureSourceScanner`) currently builds a lightweight `CSharpCompilation` from whatever assemblies happen to be loaded into the running process's `AppDomain` (`TRUSTED_PLATFORM_ASSEMBLIES` plus already-loaded assemblies). This works for early adoption but does not reliably resolve symbols across real multi-project .NET/Unity repositories: cross-project calls, package-provided types, and target-framework-specific BCL surface can all fail to bind correctly, and generated/build-output files are scanned with no exclusion. This makes method-body forbidden-call detection unreliable exactly where it matters most — before expanding it into broader source-member diagnostics (issue #104).

## What Changes

- Add a Buildalyzer/MSBuild-backed project-aware compilation context resolver in `Core.Discovery` that, for a discovered project (from `analysis.solution`/`analysis.projects`), performs an MSBuild design-time evaluation to resolve its real `Compile`-item source files and its fully resolved reference assembly paths (project references' outputs, package references, and framework references for its actual target framework).
- Wire this into method-body contract checking (`ArchitectureAnalysisSession.CheckMethodBodyContract`): matched source files are mapped to their owning discovered project, project-aware resolution is attempted, and on success its resolved source files/references are used to build the Roslyn compilation instead of the flat AppDomain-based reference list.
- Preserve the existing lightweight compilation path as an explicit fallback: when project discovery isn't configured, behavior is unchanged; when discovery is configured but project-aware resolution fails for a project (no restore, ambiguous multi-target, MSBuild error), the check falls back to the lightweight path and emits a Configuration diagnostic naming the project and the failure reason.
- Add a shared generated-file/build-output exclusion filter (bin/obj/Library/Temp/PackageCache path segments; `*.g.cs`, `*.g.i.cs`, `*.designer.cs` filenames) applied to both the fallback directory-glob source enumeration and the project-aware resolved source-file list.
- Add Buildalyzer as a new external dependency of `ArchLinterNet.Core`, confined to `Core.Discovery` by a new `external_dependencies` group and `strict_external` contracts in this repo's own self-architecture policy (`architecture/dependencies.arch.yml`), mirroring the existing DI-container confinement pattern.
- `analysis.condition_sets` preprocessor-symbol behavior is unchanged in both the project-aware and fallback paths (no merging with MSBuild's own `DefineConstants`).

## Capabilities

### New Capabilities
- `project-aware-roslyn-analysis`: Buildalyzer/MSBuild-backed per-project Roslyn compilation context resolution (source files + reference paths), owning-project mapping, and explicit project-aware-vs-fallback diagnostics.

### Modified Capabilities
- `method-body-contracts`: Roslyn semantic analysis resolves project/package/framework references through project-aware compilation context when project discovery is configured and resolution succeeds, instead of always relying on AppDomain-loaded assemblies; source file enumeration excludes generated and build-output files by default.

## Impact

- `src/ArchLinterNet.Core/Discovery/`: new Buildalyzer-backed resolver service and result records.
- `src/ArchLinterNet.Core/IO/RoslynCompilationFactory.cs`: new optional parameter path for explicit reference assembly paths.
- `src/ArchLinterNet.Core/Scanning/ArchitectureSourceScanner.cs`: accepts an optional pre-resolved source-file list and reference paths; adds shared generated/build-output exclusion.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.Checking.cs`: orchestrates owning-project mapping and project-aware vs. fallback selection for `CheckMethodBodyContract`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: registers the new resolver.
- `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj`: new `Buildalyzer` package reference.
- `architecture/dependencies.arch.yml`: new `external_dependencies` group + `strict_external` contracts confining Buildalyzer/MSBuild usage to `core_discovery`.
- Tests: `tests/ArchLinterNet.Core.Tests/` — new multi-project fixture(s) covering project references, package references, generated-code exclusion, condition sets, and fallback behavior.
