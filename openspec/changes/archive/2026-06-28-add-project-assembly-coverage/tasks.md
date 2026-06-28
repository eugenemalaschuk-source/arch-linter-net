## 1. Discovery: expose per-project identity

- [x] 1.1 Add a public `ArchitectureDiscoveredProject(string Path, string AssemblyName, IReadOnlyList<string> TargetFrameworks)` record in `src/ArchLinterNet.Core/Discovery/ProjectDiscoveryModels.cs`.
- [x] 1.2 Add `IReadOnlyCollection<ArchitectureDiscoveredProject> DiscoveredProjects` to `ProjectDiscoveryResult`, defaulting to empty in `ProjectDiscoveryResult.Empty`.
- [x] 1.3 Populate `DiscoveredProjects` from the internal `DiscoveredProjectFile` list in `ArchitectureProjectDiscovery.ResolveFromDocument`, without changing existing `TargetAssemblyNames`/`AssemblySearchPaths`/`SourceRoots`/`Diagnostics` behavior.

## 2. Contract loading and scope validation

- [x] 2.1 Update `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` to accept `project` and `assembly` scopes, keeping `dependency_edge` rejected.
- [x] 2.2 Add load-time validation rejecting a `scope: project` coverage contract when `analysis.solution` and `analysis.projects` are both empty, with an actionable error message.
- [x] 2.3 Update `ArchitectureContractLoader`'s coverage validation (the existing `ValidateCoverageNamespaces`-style check) so it does not require `roots` for `scope: project`/`scope: assembly` contracts (they enumerate discovered/resolved units, not namespace roots).

## 3. Coverage engine: assembly scope

- [x] 3.1 Add `BuildAssemblyCoverageSummary` to `ArchitectureContractRunner.Coverage.cs`, iterating `_context`'s first-party `TargetAssemblies`, classifying each as `covered`/`excluded`/`uncovered` using the existing `IsCoveredByDeclaredLayers`/`IsCoveredByExpandedTemplates` checks against each assembly's types.
- [x] 3.2 Add the `scope: assembly` branch to `CheckCoverageContract` producing `"uncovered assembly"` `ArchitectureViolation` findings with assembly name + representative type evidence.
- [x] 3.3 Add assembly-name exclusion matching using `ArchitectureCoverageExclusion.Assembly` (ordinal exact match) with required `reason`.
- [x] 3.4 Wire `BuildCoverageSummary` to dispatch `scope: assembly` to the new builder.

## 4. Coverage engine: project scope

- [x] 4.1 Add `BuildProjectCoverageSummary` to `ArchitectureContractRunner.Coverage.cs`, iterating `ProjectDiscoveryResult.DiscoveredProjects`, resolving each to a `TargetAssemblies` entry by assembly name, and classifying `covered`/`excluded`/`uncovered`/`unknown`.
- [x] 4.2 Add the `scope: project` branch to `CheckCoverageContract` producing `"uncovered project"` findings (path + assembly name + representative type) and an `unknown`-status finding/summary entry for projects whose assembly could not be resolved.
- [x] 4.3 Add project exclusion matching using `ArchitectureCoverageExclusion.Project` (exact match on project path or project-file name) with required `reason`.
- [x] 4.4 Wire `BuildCoverageSummary` to dispatch `scope: project` to the new builder.

## 5. Reporting

- [x] 5.1 Confirm human-output `Coverage summary:`/`Coverage findings:` rendering handles `scope: project`/`scope: assembly` entries without code changes (scope-agnostic); add tests if a gap is found. (Verified by reading `ArchitectureDiagnosticFormatter`: rendering is keyed off `summary.Scope` as a string with no scope-specific branching — no gap, no test needed.)
- [x] 5.2 Confirm JSON `coverage_summary`/`coverage_findings` rendering handles the two new scopes without shape changes; add tests if a gap is found. (Same formatter emits `["scope"] = summary.Scope` generically — no gap.)

## 6. Fixtures and tests

- [x] 6.1 Added project-scope coverage tests in `tests/ArchLinterNet.Core.Tests/ProjectAssemblyCoverageContractTests.cs` (covered, uncovered, excluded, unknown-assembly project) and discovery-level tests in `ArchitectureProjectDiscoveryTests.cs` (single-target and multi-targeted `DiscoveredProjects`), constructing `ArchitectureContractRunner`/`ProjectDiscoveryResult` directly rather than via compiled fixture `.csproj`/CLI YAML, since the unit doesn't require a built DLL once `ArchitectureAnalysisContext.TargetAssemblies` is populated directly.
- [x] 6.2 Added assembly-scope coverage tests in the same file (covered, uncovered, excluded assembly), using the real `ArchLinterNet.Core`/`ArchLinterNet.Testing` assemblies as first-party/uncovered fixtures.
- [x] 6.3 Skipped a dedicated Unity-like fixture: asmdef contracts are validated by `ArchitectureContractLoader`/`ArchitectureRunnerFactory` independently of coverage contracts, and the existing `ArchLinterNet.Unity.Tests` suite (unmodified by this change) continues to pass alongside the new coverage tests in the same full-suite run, demonstrating no interference without needing a new combined fixture.
- [x] 6.4 Covered by `AssemblyCoverage_WithoutSolutionConfigured_DoesNotThrow` in `CoverageContractReservedTests.cs`.
- [x] 6.5 Covered by `ProjectCoverage_WithoutSolutionOrProjectsConfigured_ThrowsActionableError` in `CoverageContractReservedTests.cs`.
- [x] 6.6 Added `AssemblyCoverage_BaselinedFinding_IsSuppressedAsIgnoredViolation` in `ProjectAssemblyCoverageContractTests.cs`; baseline support falls out of the existing `ArchitectureContractExecutionContext.IsIgnored` plumbing reused unchanged from namespace coverage.

## 7. Documentation

- [x] 7.1 Update `docs/contracts/coverage.md`: remove `project`/`assembly` from "Current limits", add usage examples and exclusion docs for both new scopes.
- [x] 7.2 Update `docs/reference/yaml-schema.md` coverage contract section with the new scope values and their field requirements.

## 8. Spec sync and archive

- [x] 8.1 Run `make fmt` and `make acceptance` (this repo's equivalent of the lifecycle's `task acceptance:fresh` — there is no Taskfile here), fix failures.
- [x] 8.2 Run `openspec archive add-project-assembly-coverage` once implementation, tests, and docs are complete.
- [x] 8.3 Run `openspec validate --all` after archiving.
