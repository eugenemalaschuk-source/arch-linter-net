## Why

ArchLinterNet's own `architecture/dependencies.arch.yml` governs a fraction of what the shipped
engine can express, and governs it through a workflow with two competing definitions of success.

Concretely, on `main` before this change:

- analysis is assembly-list driven; there is no authoritative project-discovery boundary and no
  project coverage, so a newly added `src/` project is invisible to policy until someone remembers
  to extend `target_assemblies`;
- the real shipped assembly graph (`Cli`/`Testing` → `Core` → `CEL`) is only governed at namespace
  level, so an accidental `ProjectReference` is caught only once code starts using its types;
- the shipped Core/Testing/CEL compatibility surfaces are not snapshot-governed at all, even though
  #94 and #525/#527 delivered the full capture/diff/update-preview/update lifecycle;
- `InternalsVisibleTo` sets, production→test project references, package declarations, and
  FrameworkReference adoption are entirely ungoverned;
- 26 of 28 external-dependency rules are copy-paste of the same two invariants across layers,
  predating the delivered `source_sets` capability (#465), and most of them carry no stable ID so
  they sit outside rule-input coverage;
- the seams produced by the #451/#452/#453 refactors are documentation-governed only;
- `make lint-architecture` drives self-validation through one focused `Core.Tests` regression while
  separate CLI-oriented strict/audit/JSON targets exist alongside it, so "the repository satisfies
  its own policy" has two orchestration paths and no single authoritative answer.

This is architecture-governance work over the engine that already exists. It introduces no new
user-facing contract semantics.

## What Changes

**Executable policy** (`architecture/dependencies.arch.yml`):

- `analysis.solution: ArchLinterNet.slnx` with `project_exclude` for `tests/**` and `benchmarks/**`
  becomes the authoritative project-discovery boundary, plus a `scope: project` coverage contract.
- Direct assembly governance: `strict_assembly_dependency` (CEL references no other shipped
  assembly), `strict_assembly_allow_only` (Core → CEL only; adapters → Core only),
  `strict_assembly_independence` (Cli ⟂ Testing).
- `strict_public_api_surface` with `api_comparison: exact` over reviewed snapshots for
  `ArchLinterNet.Core`, `ArchLinterNet.Testing`, and `ArchLinterNet.CEL`, stored under
  `architecture/api/`. `ArchLinterNet.Cli` is explicitly decided to be an implementation detail of
  the packed executable, not a compatibility boundary.
- `strict_project_metadata`: reviewed `allowed_friend_assemblies` per shipped project, forbidden
  `tests/**`/`benchmarks/**` project references, and `IsTestProject` forbidden on shipped projects.
- `strict_package_dependency` / `strict_package_allow_only` / `strict_framework_allow_only` for the
  boundaries that are architectural: no test framework in a shipped package, Buildalyzer/MSBuild and
  the DI container declared only by Core, CEL declaring zero packages, and only the implicit base
  runtime framework anywhere.
- The 26 copy-pasted external rules collapse into 2 authored rules expanded through named
  `source_sets` with `exclude_sources`, widening coverage to layers that had none.
- `strict_type_placement` and `strict_interface_implementation` make the post-#451/#452/#453 seams
  executable: family checkers stay in the extracted checker namespace, diagnostics and diagnostic
  payloads stay in `Core.Model`, and policy raw/document validators stay in their loading seams.
- Rule-input coverage extends to the new source-set-expanded external rules through their authored
  IDs.

**Developer workflow** (`make/lint.mk`, `make/paths.mk`, `Makefile`):

- `make lint-architecture` becomes the single authoritative read-only strict gate, running the
  canonical CLI path with `--ensure-built`. The `Core.Tests` regression stays as parity evidence in
  `make test`, not as a competing success definition.
- New thin wrappers over already-shipped CLI capabilities: `make policy-check`,
  `make public-api-check`, `make public-api-update-preview`, `make public-api-update`, and
  `make explain-architecture SOURCE=… TARGET=…`. Only `public-api-update` writes.

**One production fix, surfaced by the new self-policy**:

`ArchitectureAnalysisSession.CheckAssemblyDependencyContract` /
`CheckAssemblyAllowOnlyContract` called the id-only `IsContractSelected` overload, so selecting a
source-set-expanded assembly contract by its **authored** id silently ran nothing — contrary to the
already-agreed `source-set-expansion` requirement "The authored id selects every instance", which
the package, framework-reference, and external families already honour. Two call sites now use the
expansion-aware overload, with a pipeline regression that fails if the instances stop running. This
is a defect against an existing spec, not new capability.

**Evidence**:

- `docs/internal/self-policy-capability-matrix.md` records an adopt / already-covered / N-A / defer
  decision with rationale for every supported family, plus the engine limitations found while
  authoring (layer-glob grammar, rule-input-coverage family restriction, implicit
  `Microsoft.NETCore.App`, direct-only assembly depth, preflight under solution discovery).
- `SelfPolicyNegativeRegressionTests` mutates the real policy one guard at a time and asserts the
  mutation is caught, including that a read-only run never rewrites a snapshot.

Out of scope and unchanged: no `ArchLinterNet.Annotations` package or canonical annotation types
(#565–#574), no semantic classification block, no new contract or selector syntax, no v0.8/v0.9/v0.11
capability implementation, no persisted cross-process state, and no re-opening of #451–#453.

## Capabilities

### Modified Capabilities

- `self-architecture-policy`: gains executable governance of project discovery/coverage, the direct
  shipped assembly graph, reviewed public API surfaces, project metadata and friend assemblies,
  package/FrameworkReference boundaries, and the post-refactor structural seams; the external-rule
  authoring model becomes source-set expansion; and the canonical read-only gate is redefined as the
  CLI path, with the Testing regression restated as parity evidence.

## Impact

- `architecture/dependencies.arch.yml`: new `packages`, `framework_references`, `source_sets`
  sections; `analysis.solution`/`project_exclude`; new contract groups; external rules collapsed.
- `architecture/api/*.public-api.txt`: three new reviewed snapshots (generated, not hand-edited).
- `make/lint.mk`, `make/paths.mk`, `make/test.mk`, `Makefile`, `AGENTS.md`, `.gitignore`.
- `tests/ArchLinterNet.Core.Tests/`: `SelfArchitecturePolicyTests` (now ensure-built, E2E bucket),
  new `SelfPolicyNegativeRegressionTests` and `SelfPolicyRepository`.
- `docs/internal/self-policy-capability-matrix.md`: new.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyDependency.cs`: the only
  production change — two contract-selection call sites switched to the expansion-aware overload.
- `tests/ArchLinterNet.Core.Tests/SourceExpansionPipelineIdentityTests.cs`: regression for that fix.
