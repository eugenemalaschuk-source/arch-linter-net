## 1. Self-policy and dependency setup

- [x] 1.1 Add `Buildalyzer` `PackageReference` to `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj` (also pins `Microsoft.CodeAnalysis.VisualBasic` and `System.Security.Cryptography.Xml` explicitly to resolve a NuGet version-conflict/audit error surfaced by Buildalyzer's transitive dependencies)
- [x] 1.2 Add `msbuild_project_evaluation` external dependency group (`Buildalyzer`, `Microsoft.Build` namespace prefixes) to `architecture/dependencies.arch.yml`
- [x] 1.3 Add `strict_external` contracts confining `msbuild_project_evaluation` to `core_discovery`, mirroring the existing `dependency_injection_container` confinement contracts
- [x] 1.4 Update `self-policy-rule-input-coverage` contract_ids list in `architecture/dependencies.arch.yml` if the new contracts need tracking (not needed — the new contracts follow the existing `strict_external` pattern, which isn't rule-input-tracked either)

## 2. Generated-file exclusion filter

- [x] 2.1 Add a shared exclusion helper in `Core.Scanning` (`ArchitectureGeneratedFileFilter`) excluding `bin`/`obj`/`Library`/`Temp`/`PackageCache` path segments and `.g.cs`/`.g.i.cs`/`.designer.cs` filename suffixes
- [x] 2.2 Apply the filter in `ArchitectureSourceScanner.FindSourceFilesForNamespace`'s directory enumeration, checked against the path *relative to the scanned source root* (an absolute-path check regressed two existing tests whose fixtures live under the OS temp directory, itself containing a `Temp` segment)
- [x] 2.3 Add unit tests for the filter: build-output dirs, Unity-generated dirs, generated filename suffixes, ordinary files unaffected

## 3. Project-aware compilation context resolver (Core.Discovery)

- [x] 3.1 Define `ArchitectureProjectRoslynContext` (project path, resolved source file paths, resolved reference assembly paths) and a failure-result type in `Core.Discovery`
- [x] 3.2 Define `IArchitectureProjectRoslynContextResolver` and a Buildalyzer-backed implementation that runs an MSBuild design-time build for a given discovered project
- [x] 3.3 Handle design-time build failure (missing restore, MSBuild errors, ambiguous multi-target) by returning a failure result with a reason, never throwing
- [x] 3.4 Register the resolver in `AddArchLinterNetCore()` (`src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`)
- [x] 3.5 Add tests: successful resolution against a real multi-project fixture; failure due to missing restore; failure due to nonexistent project file

## 4. Wire project-aware resolution into method-body checking

- [x] 4.1 Extend `IRoslynCompilationFactory.Create`/`RoslynCompilationFactory` with an optional explicit reference assembly paths parameter, used instead of the AppDomain-based reference list when provided
- [x] 4.2 Pass an optional explicit reference assembly path list from `ArchitectureSourceScanner.FindMethodBodyViolations` through to `IRoslynCompilationFactory.Create` (source file selection is unchanged — still the existing namespace-filtered list, now with the generated-file filter applied)
- [x] 4.3 In `ArchitectureAnalysisSession.CheckMethodBodyContract`, map matched source files to their single owning discovered project via directory containment against `Context.ProjectDiscovery.DiscoveredProjects`
- [x] 4.4 Attempt project-aware resolution for the owning project when discovery is configured and exactly one owning project is found; use its resolved references when successful
- [x] 4.5 Fall back to the existing lightweight compilation when discovery isn't configured, no/multiple owning projects are found, or resolution fails
- [x] 4.6 Emit a fallback notice as an `ArchitectureViolation` embedded directly in the contract's own returned violation list (kind `"project-aware analysis fallback"`) naming the project and failure reason — not routed through `Context.DiscoveryDiagnostics`, since that collection is fixed before any contract check runs (see design.md decision 4)
- [x] 4.7 Confirm no diagnostic is produced when discovery isn't configured, and none when project-aware resolution succeeds

## 5. Tests (acceptance criteria coverage)

- [x] 5.1 Multi-project fixture: method-body contract in project A calls a forbidden member on a type from project B (project reference) — verify project-aware resolution finds it
- [x] 5.2 Multi-project fixture: method-body contract calls a forbidden member from a NuGet package reference — verify project-aware resolution finds it
- [x] 5.3 Generated-code exclusion: fixture with build-output-directory and `.g.cs` files containing a forbidden call — verify neither is reported
- [x] 5.4 Condition sets: verify `analysis.condition_sets` still gates `#if` blocks identically in the project-aware path (symbol defined/undefined); the fallback path's condition-set behavior is already covered by the pre-existing `ConditionSetConfigTests` suite, unmodified by this change
- [x] 5.5 Fallback behavior: covered by the full pre-existing test suite passing unchanged (695 passing vs. 673 on `main`, same 5 pre-existing environment-flaky failures) — no repository in that suite configures `analysis.solution`/`analysis.projects` for method-body contracts, so it exercises exactly the "discovery not configured" path
- [x] 5.6 Fallback diagnostic: verify a project with discovery configured but no restore/build output produces the fallback diagnostic and still reports violations via the lightweight path

## 6. Spec sync and validation

- [x] 6.1 Run `openspec validate --changes project-aware-roslyn-analysis --strict` and fix any issues
- [x] 6.2 Run `make fmt` (`dotnet format` made no changes beyond what was already written; `mdformat` reformatted unrelated pre-existing docs files with no content changes from this feature, reverted as out of scope)
- [x] 6.3 Run `make acceptance`: `dotnet build` for Cli/Unity succeeds; full solution `dotnet test` is 697/702 Core.Tests, 63/63 Cli.Tests, 3/3 Unity.Tests. The 5 Core.Tests failures are a pre-existing environment blocker in this sandbox (confirmed identical on `main` via an isolated worktree — `ArchitectureRepositoryRootResolverTests`/`ArchitectureProjectDiscoveryServiceFakeFileSystemTests`/one `ArchitectureSourceScannerFakeSeamTests` case fail due to working-directory resolution in this sandbox, unrelated to this change). `lint-docs` also fails in this sandbox (`python3` not resolvable) — a pre-existing tooling gap, not touched by this change.
- [x] 6.4 Run `openspec archive project-aware-roslyn-analysis` once implementation, tests, and validation are complete
- [x] 6.5 Run `openspec validate --all` after archiving to confirm all specs remain valid (62 passed, 0 failed)
