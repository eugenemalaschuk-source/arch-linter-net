## Context

`ArchitectureContractRunner.Coverage.cs` already implements `scope: namespace` and `scope: rule_input` coverage by walking `ArchitectureCoverageInventory.Namespaces` and checking `IsCoveredByDeclaredLayers`/`IsCoveredByExpandedTemplates`. `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` rejects any other scope before a run starts.

Two existing data sources already carry what `scope: project`/`scope: assembly` need, but neither exposes it in a coverage-ready shape:

- `ArchitectureAnalysisContext.TargetAssemblies` is `IReadOnlyCollection<System.Reflection.Assembly>` — the resolved first-party assemblies for the run. Each `Assembly` already exposes `.Location` (path) and `.GetName().Name` (identity), and its types can be grouped into namespaces the same way `ArchitectureCoverageInventory` already does for the namespace scope. This is available whenever assembly resolution succeeds, with or without `analysis.solution`/`analysis.projects`.
- `ArchitectureAnalysisContext.ProjectDiscovery` (`ProjectDiscoveryResult`) currently only exposes flattened `TargetAssemblyNames`/`AssemblySearchPaths`/`SourceRoots`/`Diagnostics` — the per-project identity (`DiscoveredProjectFile`: `AbsolutePath`, `AssemblyName`, `TargetFrameworks`) is computed internally by `ArchitectureProjectDiscovery` but discarded before it reaches `ProjectDiscoveryResult`. `scope: project` coverage needs that per-project identity (path, name) to exist as a unit, so this needs a small additive change to discovery's public result shape, not a new discovery mechanism.

`.asmdef` coverage is a deliberately separate, already-shipped mechanism (`ArchitectureAsmdefContract`/`ArchitectureAsmdefScanner`) scanning `Assets/**/*.asmdef`. It has its own identity model (asmdef name + references) that has nothing to do with `.csproj`/assembly discovery, and issue #99 explicitly requires the two stay distinct — this design does not touch it.

## Goals / Non-Goals

**Goals:**
- Implement `scope: assembly` coverage against `ArchitectureAnalysisContext.TargetAssemblies` (works for any run with resolved first-party assemblies, regardless of how they were resolved).
- Implement `scope: project` coverage against discovered `.csproj` projects, reusing `ArchitectureProjectDiscovery`'s per-project identity (newly exposed on `ProjectDiscoveryResult`), requiring `analysis.solution` and/or `analysis.projects` to be configured.
- Reuse the existing coverage-provider rule (declared layers, namespace-glob layers, expanded layer templates) unchanged — a project/assembly is `covered` when at least one type inside it matches a layer provider, exactly like a namespace is.
- Reuse the existing `exclude`/`reason` mechanism, now activating the already-modeled but previously-dead `ArchitectureCoverageExclusion.Project`/`.Assembly` fields.
- Extend `ArchitectureCoverageSummary`/`coverage_findings`/`coverage_summary` to carry project/assembly entries without changing their JSON shape (same `counts`, `excluded_items`, `uncovered_items` fields already used by namespace scope).

**Non-Goals:**
- `scope: dependency_edge` coverage (stays reserved/rejected).
- Any change to `.asmdef` scanning, `ArchitectureProjectDiscovery`'s build-output/staleness resolution logic, or `analysis.target_assemblies`/`analysis.assembly_search_paths` precedence.
- Full assembly dependency contracts, NuGet leakage checks, runtime DI validation (explicit issue non-goals).
- Cross-project/cross-assembly relationship checks — this is per-unit coverage classification only, the same granularity namespace coverage already has.

## Decisions

### Decision: `scope: assembly` units come from `ArchitectureAnalysisContext.TargetAssemblies`, not from discovery
Every run that completes assembly resolution already has a concrete, first-party `Assembly` list regardless of whether discovery was used. Building assembly coverage from `TargetAssemblies` means `scope: assembly` works identically whether the policy uses explicit `analysis.target_assemblies` or `analysis.solution`/`analysis.projects` — matching the issue's "remain compatible with existing explicit `target_assemblies`... behavior" requirement. Each assembly's "representative" evidence is the first type (by declared namespace, ordinal) found inside it, mirroring `ArchitectureCoverageNamespaceEntry.RepresentativeType`.
**Alternative considered:** deriving assembly units from `ProjectDiscovery` only — rejected because it would make `scope: assembly` silently produce zero units (not an error, just an empty, never-failing contract) for any policy using explicit `target_assemblies` without `analysis.solution`/`projects`, which is the common case today and would be a confusing silent no-op.

### Decision: `scope: project` units come from a newly-exposed `ProjectDiscoveryResult.DiscoveredProjects`
`ArchitectureProjectDiscovery` already computes `DiscoveredProjectFile(AbsolutePath, AssemblyName, TargetFrameworks)` per project; it's currently `internal` and never returned. Add a new public record, e.g. `ArchitectureDiscoveredProject(string Path, string AssemblyName, IReadOnlyList<string> TargetFrameworks)`, and a new additive field `ProjectDiscoveryResult.DiscoveredProjects` populated from the same internal list. This is additive — no existing field changes shape — so `#56` discovery behavior, diagnostics, and backward compatibility are untouched.
A `scope: project` contract requires `analysis.solution` or `analysis.projects` to be set; if neither is configured, `DiscoveredProjects` is empty and the contract has no in-scope units. Rather than silently passing (which would defeat the issue's "strict coverage fails when discovered ... projects ... are uncovered" guarantee), the loader rejects a `scope: project` contract when no project discovery is configured, with an actionable message — symmetric to how `rule_input` coverage already requires `contract_ids` to point at real contracts.
**Alternative considered:** deriving project units from `.csproj` files found by scanning the repo directly, independent of `analysis.solution`/`projects` — rejected as out of scope; it would duplicate `#56`'s discovery logic instead of building on it, which the issue explicitly asks for.

### Decision: a project's coverage status is derived from its resolved assembly's types
Once a `DiscoveredProjectFile`'s `AssemblyName` is matched against `TargetAssemblies` (by `Assembly.GetName().Name`, ordinal-insensitive-free exact match — same comparer style discovery already uses), `scope: project` coverage reuses the exact same "does any type's namespace match a layer provider" check as `scope: assembly`. If a discovered project's assembly name has no match in `TargetAssemblies` (filtered out, build output missing/stale, multi-target ambiguous), the project is classified `unknown` rather than `uncovered` — consistent with `architecture-coverage-model`'s existing distinction ("`unknown` ... when required discovery input is unavailable or ambiguous").
**Alternative considered:** classifying an unresolved project as `uncovered` — rejected because `uncovered` is reserved for "matched no coverage provider", a different condition from "could not even be classified."

### Decision: reuse `ArchitectureCoverageExclusion.Project`/`.Assembly` exactly as already modeled
These fields exist today, unused. `scope: project` exclusions match on project path or project-file name (case-insensitive exact match, mirroring how `analysis.project_exclude` globs already work conceptually but kept as exact-match here since there's no existing glob matcher for paths in the exclusion shape); `scope: assembly` exclusions match on assembly name (ordinal). Each exclusion still requires a non-empty `reason`, enforced the same way namespace/rule-input exclusions already are.
**Alternative considered:** introducing glob matching for project/assembly exclusions — deferred; exact-match keeps parity with how `ArchitectureCoverageExclusion` already treats `ContractId` (exact match) rather than inventing a new matcher family for this change.

### Decision: new finding kinds `"uncovered project"` / `"uncovered assembly"`, summary scopes `"project"`/`"assembly"`
`CheckCoverageContract` gains two new branches (mirroring the existing namespace branch) producing `ArchitectureViolation` findings with `ForbiddenReference` = `"uncovered project"` or `"uncovered assembly"`, and `BuildCoverageSummary` dispatches to new `BuildProjectCoverageSummary`/`BuildAssemblyCoverageSummary` methods returning the existing `ArchitectureCoverageSummary` shape with `Scope` set to `"project"`/`"assembly"`. No changes to the JSON/human rendering layer are needed beyond what already iterates "every coverage contract that ran" — it is scope-agnostic.

## Risks / Trade-offs

- **[Risk]** Matching a discovered project to a resolved assembly purely by assembly-name string could mis-attribute coverage if two projects in a repo share an `AssemblyName` (unusual but possible in generated/sample layouts). → Mitigation: this mirrors how assembly resolution itself already works (`target_assemblies` is name-keyed); no new ambiguity class is introduced, and existing multi-target/staleness diagnostics from `#56` already surface ambiguous cases as `unknown` here.
- **[Risk]** Requiring `analysis.solution`/`analysis.projects` for `scope: project` contracts is a new load-time constraint that could surprise an author who copies a `scope: project` example into a policy that only sets `target_assemblies`. → Mitigation: the rejection message names exactly which `analysis.*` fields to set, matching the existing reserved-scope error's actionable style; documented prominently in `docs/contracts/coverage.md`.
- **[Trade-off]** `scope: project` and `scope: assembly` can report different coverage for the "same" code when project↔assembly mapping is 1:1 but discovery and resolution disagree on what's in scope (e.g., `project_exclude` removed a project from discovery but its prebuilt DLL is still in `assembly_search_paths`). This is intentional: the two scopes classify different units (source projects vs. resolved binaries) and a reviewer who cares about both should declare both contracts, exactly as `architecture-coverage-model` keeps `namespace`/`project`/`assembly`/`dependency_edge` as distinct discriminant values rather than aliases of each other.

## Migration Plan

No migration: this only legalizes two previously-rejected scopes and is purely additive to the schema, discovery result shape, and coverage engine. Existing policies that declare no `scope: project`/`scope: assembly` contracts are unaffected. Policies that already (incorrectly) declared those scopes were already failing validation with the reserved-scope error; they now succeed once the contract is well-formed, which is the intended unblocking, not a behavior change requiring a flag.

## Open Questions

- Should `scope: project` exclusions support a glob/prefix match on project path (consistent with `analysis.project_exclude`) instead of exact match, in a later follow-up? Left as exact-match for this change to keep the exclusion matcher family minimal; revisit if real policies need it.
