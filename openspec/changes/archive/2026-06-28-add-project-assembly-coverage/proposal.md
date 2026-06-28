## Why

Namespace coverage (`scope: namespace`) catches uncovered code inside namespaces a policy author already rooted, but it cannot see whole first-party projects or assemblies that were never wired into any root at all — a new `.csproj` added to the solution, a renamed assembly, or a Unity-style assembly definition can sit completely outside policy enforcement and nobody is told. `architecture-coverage-model` already reserves `scope: project` and `scope: assembly` for this and `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` explicitly rejects them today — issue #99 is the follow-up that implements them on top of the project/assembly discovery (`#56`) that now exists.

## What Changes

- Accept `scope: project` and `scope: assembly` in `strict_coverage`/`audit_coverage` contracts instead of rejecting them at load time.
- Classify discovered first-party projects (from `ArchitectureProjectDiscovery`) and resolved first-party assemblies (from `ArchitectureAnalysisContext`) as `covered` when at least one type they contain matches a declared layer, namespace-glob layer, or expanded layer-template layer — the same coverage-provider rule namespace coverage already uses.
- Honor `exclude` entries using the existing (already-modeled but previously unused) `project`/`assembly` exclusion fields, each requiring a `reason`.
- Emit `uncovered project` / `uncovered assembly` coverage findings carrying project/assembly identity, file path when available, and a representative namespace/type, and fold them into the existing `coverage_findings` and `coverage_summary` output (human + JSON) alongside namespace/rule-input results.
- Update `docs/contracts/coverage.md` and `docs/reference/yaml-schema.md` to document the two new scopes and drop them from the "reserved" list.
- Leave Unity `.asmdef` contracts (`ArchitectureAsmdefContract`/`ArchitectureAsmdefScanner`) untouched — they remain a separate mechanism, not folded into generic project/assembly coverage.

## Capabilities

### New Capabilities
- `project-coverage-contracts`: `scope: project` coverage classification, exclusions, and findings for discovered first-party `.csproj` projects.
- `assembly-coverage-contracts`: `scope: assembly` coverage classification, exclusions, and findings for resolved first-party assemblies.

### Modified Capabilities
- `namespace-coverage-contracts`: the "Unsupported coverage scopes are rejected" requirement (which lives on this capability) no longer reserves `scope: project`/`scope: assembly` — only `dependency_edge` stays reserved after this change.
- `architecture-coverage-reporting`: `coverage_summary`/`coverage_findings` now also include entries for `scope: project` and `scope: assembly` contracts; the "Reserved scopes are not summarized" scenario is narrowed to `dependency_edge` only.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`: `ValidateImplementedCoverageScopes` accepts `project`/`assembly`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.Coverage.cs`: new `BuildProjectCoverageSummary`/`BuildAssemblyCoverageSummary` (or equivalent) alongside existing namespace/rule-input builders.
- `src/ArchLinterNet.Core/Reporting/ArchitectureCoverageSummary.cs` and the coverage finding model: no new shape, reused for the two new scopes.
- `src/ArchLinterNet.Core/Discovery/*`: consumed read-only for project identity/path; no discovery behavior changes (`#56` stays intact).
- `docs/contracts/coverage.md`, `docs/reference/yaml-schema.md`: documentation updates.
- New test fixtures and YAML test policies under `tests/ArchLinterNet.Core.Tests` and `tests/ArchLinterNet.Cli.Tests/TestPolicies`.
- No changes to `ArchitectureAsmdefContract`/`ArchitectureAsmdefScanner` or `analysis.target_assemblies`/`analysis.assembly_search_paths` backward compatibility.
