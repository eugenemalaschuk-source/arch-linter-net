# Changed-project advisory analysis and dependency-aware invalidation evidence (#503)

## Decision

This is an evidence-first measurement and design gate. No changed-file-only validation path, scope
planner, or invalidation cache is implemented by this issue, and no output from this task weakens
full strict validation as the authoritative result.

The checked-in machine-readable artifacts are
[`changed-project-advisory-analysis-results.json`](changed-project-advisory-analysis-results.json) and
the explicit secondary timing artifact
[`changed-project-advisory-analysis-timing-results.json`](changed-project-advisory-analysis-timing-results.json).
Unlike the wall-clock benchmark evidence for #493/#655/#461, the closure numbers here are **exact
deterministic graph computations**, not timed samples: every row is reproduced by an assertion in
[`ChangedProjectAdvisoryScopePlanningTests.cs`](../../tests/ArchLinterNet.Core.Tests/ChangedProjectAdvisoryScopePlanningTests.cs),
which runs in every normal `make test` pass (no `[Explicit]` exclusion — there is no hardware
sensitivity to isolate).

**Outcome: C — unsafe implementation boundary.** The change-to-project *disposition* mapping (Question 2) — given a changed
input's kind and its already-resolved owning project(s), which scope-widening rule applies — is
complete and exact. The ownership evidence now exercises the authoritative Buildalyzer/MSBuild
evaluated `@(Compile)` basis for ordinary, explicit include/exclude, single-owner linked, and
multi-owner shared files, but it remains a test seam rather than a production scope resolver; see the
correction in Question 2. Dependency
closure (Question 3) is complete for the reference-graph dimension **and** now includes
real, code-grounded evidence for a representative evaluator/fact-family subset — #503 explicitly
forbids assuming one graph direction serves every contract family, and an earlier revision of this
document did exactly that (see the correction in Question 3). The scope-plan/coverage/advisory-
semantics design (Questions 4–6) is complete as a design sketch but, like Question 3, is proven only
for that representative subset; every remaining schema family now receives an explicit conservative
full-population fallback through `PlanContractFamily`, rather than inheriting an unreviewed graph
direction. The timing artifact records three successful full-strict profiles at each of S/M/L
synthetic #502 staged-assembly scales and an explicitly optimistic K/P upper-bound model. Only the
measured `contract_checks` phase is treated as potentially project-scalable, and the model assumes
all of that phase scales linearly; every other phase is held unscaled because its project-scope
sensitivity is unproven. It does not execute an incremental path or measure realized savings. The
combination is enough to conclude that no safe implementation boundary is established: modeled
upper-bound leaf reductions are 6.832–10.955% on this declared synthetic boundary, middle-position
upper bounds are 3.187–4.695%, and global/shared changes fall back to full validation. Those estimates
are descriptive; see
[Required decision outcome](#required-decision-outcome) for the safety basis.

## P0 consumer-normalization gate (#991)

Per #503's own routing note, this evidence/design task may proceed while consumer CI is normalized,
but no incremental-analysis implementation child may start solely from the pre-normalization dogfood
latency table recorded in #991 (`arch-linter-net` self-CI 204s, `firstice-server` 1073s, `firstice`
Unity 41s, `firstice-map-editor` 249s). This task therefore:

- records the deterministic closure evidence and scope-plan contract now, since that evidence is
  independent of consumer CI topology;
- records Outcome C and does **not** create a focused implementation issue under #19 — the timing
  artifact is complete as secondary evidence but is a modeled full-strict comparison rather than
  partial execution evidence, and #991 disqualifies the pre-normalization dogfood latency as sole
  implementation justification;
- records the exact re-evaluation trigger: after #991 reaches its decision gate, complete the
  timing/effect gate against normalized dogfood spans before deciding whether to open the child issue.

## Methodology

- Reuse the #502 `large-solution-benchmark/v1` synthetic project-graph generator
  (`BenchmarkWorkloadGenerator`, `BenchmarkGraphBuilder`) with `ReferencesPerProject = 0` so each
  workload's edges are exactly its named topology shape, with no added density edges.
- Implement `ChangedProjectScopePlanner` (test-only, `tests/ArchLinterNet.Core.Tests/Benchmarking/`)
  as a pure function from `(projects, edges, changed inputs)` to a scope plan: per-input disposition
  plus the union of transitive dependent closures. It does not run analysis, so it carries none of
  the coverage-accounting or preview/execution-parity guarantees a later production implementation
  would need — it exists only to make the mapping and closure questions measurable.
- Edge direction follows the #502 convention: `FromProjectId` depends on `ToProjectId`. The affected
  set for a changed project is its transitive **dependents** (reverse reachability), because those
  are the projects whose own validation could observe the change.
- Measure four of #502's five buildable graph shapes (`Linear`, `WideFanOutFanIn`, `Diamond`,
  `Dense`); `CyclicScc` is excluded because it is a structural-only descriptor in #502's v1 catalog
  (no buildable project-reference cycle), and this evidence task needs no analyzer execution.
- Vary project count independently at four sizes (8/16/32/64) to observe how the closure size `K`
  grows relative to `P`, per representative changed-input position, rather than assuming affected
  scope stays small.
- Separately, classify the last 300 commits on this repository's own `main` history by file path,
  using the checked-in, reproducible
  [`tools/scripts/classify_pr_traffic_mix.py`](../../tools/scripts/classify_pr_traffic_mix.py), to get
  a real (if approximate) PR-traffic-mix prior complementing the synthetic closure math with evidence
  about how often this repository's own changes touch globally-scoped inputs. Reproduce with:
  `uv run --project tools/pyproject.toml python tools/scripts/classify_pr_traffic_mix.py --end-ref bb533f0f2fc0d2e30addc7a4110adda26e825e68 --commit-count 300`.
- Run the explicit `ChangedProjectAdvisoryEffectBenchmarkHarness` against the same #502 foundation
  at 8, 16, and 32 staged synthetic projects, with three successful full-strict
  `analysis-profile/v1` samples at each scale. The harness holds every measured phase unscaled except
  `contract_checks`, which is treated as wholly project-scalable only to produce an optimistic upper
  bound; `load_and_setup`, `build_state_preflight`, `post_processing`, `repository_metrics`, and all
  other phases stay unscaled. This is not a measured decomposition of incremental work. It measures
  pure planner overhead and calculates K/P upper bounds without executing a partial analyzer path. It requires the
  full SHA of the clean checked-out source commit in `ARCH_LINTER_SOURCE_SHA`. Reproduce with:
  `ARCH_LINTER_SOURCE_SHA=$(git rev-parse HEAD) dotnet test tests/ArchLinterNet.Core.Tests --no-restore --filter FullyQualifiedName~ChangedProjectAdvisoryEffectBenchmarkHarness`.

## Environment

Closure measurements are exact graph computations with no environment sensitivity: the same input
graph always produces the same closure on any machine, .NET version, or OS. No wall-clock,
processor-time, or allocation sampling applies to this evidence, unlike the #493/#655/#461 harnesses.
The repository-history classification is reproducible and pinned to an exact range: the 300 commits
ending at `bb533f0f2fc0d2e30addc7a4110adda26e825e68` (2026-09-22) and starting at
`3cb0cf5bc996101c41ac9b85a9a0434b5c7b5f52` (2026-07-20), extracted by
`tools/scripts/classify_pr_traffic_mix.py` (see [Methodology](#methodology) for the exact
invocation); raw output is in
[`changed-project-advisory-analysis-results.json`](changed-project-advisory-analysis-results.json).
The timing harness retains its raw profiles in
[`changed-project-advisory-analysis-timing-results.json`](changed-project-advisory-analysis-timing-results.json).

## Question 1 — User outcome

An advisory path that only re-validates a changed project's transitive dependents is a materially
smaller unit of work **only for specific change shapes**, not universally:

- A change to a project with few or no dependents (`K=1` in every measured shape at every size)
  validates one project instead of `P`.
- A change to a single "spoke" in a wide fan-out/fan-in graph stays at a **constant** `K=2`
  regardless of `P` — the affected-scope ratio shrinks toward zero as the solution grows
  (`K/P = 2/64 ≈ 3.1%` at the largest measured size). This is the one shape where scale itself makes
  the opportunity bigger, not smaller.
- A change to a project near the middle of a linear chain, a densely-connected graph, or the tail of
  a diamond consistently affects **about half** the solution (`K/P ≈ 0.51–0.63` across 8–64
  projects) — a bounded, scale-independent ~2x ceiling, not an asymptotic win.
- A change to the shared foundation of any measured shape, or to any input classified as global or
  unmappable (central build/package props, analyzer/generator/additional files, policy/import
  changes, generated output/build context), reaches `K=P` — full fallback, zero advisory benefit.

Repository-history evidence (300 commits ending at `bb533f0f`, path-pattern classification,
reproducible via `tools/scripts/classify_pr_traffic_mix.py`, raw output in
[`changed-project-advisory-analysis-results.json`](changed-project-advisory-analysis-results.json)):
31/300 (10.3%) of commits touch a central-build-props or policy path that would force global
fallback under these rules; 15/300 (5.0%) touch only compiled project source (`.cs`) and/or
`.csproj` paths (a clean scoped case — corrected after review: the classifier originally counted any
file under `src/`/`tests/` regardless of extension, which folded non-compiled files like JSON test
fixtures into this bucket and overstated it at 18/300). The remaining ~85% mix project source with
docs/OpenSpec/non-`.cs`-under-`src`-or-`tests`/other paths whose architecture-lint relevance this
task does not resolve — a correct implementation would need to classify those as excluded (no
re-analysis needed) or direct-mapped, not silently folded into "global." That mapping question is
exactly what a focused implementation issue would need to answer with real path-ownership rules
(§2b), not path-pattern heuristics. This traffic-mix reading is informal, approximate context: it
classifies raw commit file lists by path pattern, not a literal run of `ChangedProjectScopePlanner`
(which needs a structured project/edge graph the commit history does not provide).

**Conclusion**: material user-outcome benefit exists, but only for a bounded subclass of PR-shaped
changes (leaf/spoke-shaped, low-dependent-count changes); it is not a general "PR feedback gets
faster" claim.

## Question 2 — Change-to-project mapping

### 2a. Disposition given known ownership

`ChangedProjectScopePlanner` gives every changed-input kind from #503's own list one explicit,
tested disposition. No kind is silently dropped. **This table answers "given that a changed input is
already known to be owned by project(s) X, what scope-widening rule applies" — it does not answer
"given a raw changed file path, which project(s) actually own it." That second, prior step is a
separate, unaddressed problem; see the correction in §2b.**

| Changed input kind | Disposition | Reason |
|---|---|---|
| Project-owned source file | `DependencyDrivenExpansion` | Owned by exactly one project; scope widens to that project's transitive dependents. |
| Shared/linked source file (1+ owning projects, via MSBuild `Link`) | `DependencyDrivenExpansion` | Being "linked" describes how ownership was resolved (evaluated `@(Compile)` items, not directory containment), not a minimum owner count — a single project can link a file from outside its own directory. Scope starts from every owning project and widens to their combined dependents. |
| Project-reference edge change (`.csproj` reference add/remove) | `DependencyDrivenExpansion` | Both endpoints are seeded; an edge's presence/absence can change graph-shaped contract results for either side. |
| `.csproj` property edit (non-reference) | `DependencyDrivenExpansion` | Same rule as an owned source file. |
| Package/framework reference change (one project) | `DependencyDrivenExpansion` | Same rule as an owned source file. |
| `Directory.Build.*` / `Directory.Packages.props` / `.editorconfig` / `NuGet.config` / `global.json` | `GlobalExpansion` | In this repository these files live at the repository root, so MSBuild/NuGet's directory-ancestry resolution currently makes every project inherit them — see the repository-specific caveat below. |
| Analyzer/generator/additional file | `GlobalExpansion` | Can change compiled output for any consuming project in ways static reference analysis cannot verify. |
| Policy/import file (`architecture/*.yml`) | `GlobalExpansion` | Can change selector membership, layer boundaries, or contract scope for any project. |
| Baseline / reviewed public-API snapshot (`architecture/api/*.public-api.txt`) | `UnmappableFallback` | A `strict_public_api_surface` contract binds one `api_snapshot` to an `assemblies` list that is schema-unbounded (`schema/dependencies.arch.schema.json` `publicApiSurfaceContract`), so one snapshot can govern more than one project's output. Without a deterministic contract-to-assemblies-to-projects mapping, a single owning project cannot be assumed — see caveat below. |
| Generated output / build-context change | `UnmappableFallback` | No reviewed static ownership mapping exists; the input is unmappable and falls back to full scope rather than being excluded. |

Caveat on the shared/linked-source-file row (revised after review): this evidence task originally
required at least two owning projects for this kind, rejecting a single-owner linked file. Per
Context7's MSBuild documentation, `<Compile Include="…" Link="…" />` can be used by exactly one
project to compile a file that physically lives outside that project's own directory — "linked"
describes *how* ownership was resolved (evaluated `@(Compile)` items), not a minimum owner count. A
future ownership resolver built on evaluated project items (§2b) will legitimately produce
single-owner linked inputs, which the original `Count < 2` constraint would have rejected or forced
into `ProjectOwnedSourceFile` misclassification. `ChangedProjectScopePlanner` now accepts one or more
owning projects for this kind (see `LinkedSourceFileChange_AcceptsASingleOwningProject` and
`SharedSourceFileChange_UnionsMultipleOwningProjectsDependents` in
`ChangedProjectAdvisoryScopePlanningTests.cs`).

Caveat on the `Directory.Build.*`/central-props row: MSBuild and NuGet resolve these files by
directory-ancestry/nearest-file search, not by an inherent "applies everywhere" rule — a future
multi-root or nested-`Directory.Build.props` layout could scope one of these files to a subtree
rather than the whole solution. Today, in this repository, `Directory.Build.props`,
`Directory.Build.targets`, and `Directory.Packages.props` all live at the repository root with no
nested overrides, so `GlobalExpansion` is currently correct as well as conservatively safe. A
production implementation must resolve the actual nearest-file ancestry per project rather than
assume repository-root universality as a general architectural guarantee.

Caveat on the API-snapshot row (revised after review): this evidence task originally treated the row
as project-scoped, assuming one snapshot always names exactly one project. Checking the actual
`publicApiSurfaceContract` schema shows `assemblies` is an unbounded list — today's three concrete
policy entries in `architecture/policy/public-api-and-coverage.arch.yml` each happen to name exactly
one assembly, but the schema does not guarantee that, so treating the row as narrower than
`GlobalExpansion`/`UnmappableFallback` would make the evidence boundary narrower than the product
proves. `ChangedProjectScopePlanner` now classifies this kind as `UnmappableFallback` until a
deterministic contract-to-assemblies-to-projects mapping exists (see
`ApiSnapshotChange_DoesNotAssumeExactlyOneOwningProject` in
`ChangedProjectAdvisoryScopePlanningTests.cs`), and it is **not** included among the narrow supported
change classes in [Required decision outcome](#required-decision-outcome).

Every measured decision carries a human-readable reason string (asserted by
`GlobalOrUnmappableInputs_WidenToTheFullProjectPopulation` and
`EveryChangedInput_ReceivesExactlyOneDeterministicDecision`); none of the ten kinds above returns an
implicit "no-op" or silently excludes an input.

### 2b. Ownership resolution is evaluated-item evidence, not a production planner

`ChangedInput.OwningProjectIds` is still a field the scope-planner tests supply directly — for the
synthetic corpus, the test author already knows which project a generated path belongs to, because
`BenchmarkWorkloadGenerator` produced that path deterministically in the first place. The focused
`ArchitectureProjectRoslynContextResolverTests` now exercise the authoritative evaluated-item seam
for ordinary project-owned files, explicit include/exclude, a single-owner linked file, and a shared
multi-owner linked file. This proves the evidence basis without wiring a new production resolver into
incremental execution; the planner tests still do not compute ownership from a path prefix;
§2a's table and tests exercise only the disposition step that runs *after* ownership is already
known. Presenting §2a as answering the whole of #503's "change-to-project mapping" question would
overstate what is proven — this correction narrows that claim explicitly.

The real authoritative basis for that resolution step is not directory containment or path-pattern
matching. A real MSBuild project's compiled surface is its *evaluated* project items —
`@(Compile)` after glob expansion, including any `<Compile Include="…" Link="…" />` entries that pull
in a file from an arbitrary location outside the project's own directory (the mechanism a real
linked/shared source file uses), plus whatever `Directory.Build.props`/explicit `<Import>` chain
contributed to that evaluation. A file's location on disk is not proof of which project(s) compile
it, and a naive path-prefix classifier — such as this task's own
[`classify_pr_traffic_mix.py`](../../tools/scripts/classify_pr_traffic_mix.py), already labeled
informal/approximate for a different reason in Question 1 — is not a substitute for that evaluation.
This makes the limitation more fundamental than "approximate": path-prefix matching is not the
authoritative model MSBuild itself uses, for any repository where compiled items are not implicitly
"everything under the project's own directory" (globs with explicit exclusions, generated files,
linked files from a shared location, and similar are all common in practice, if not in this
repository today).

A production implementation's ownership resolver would need to evaluate the actual project files
(via MSBuild, the same way build-state preflight and project discovery already do elsewhere in this
codebase) to produce a real `path → owning project(s)` mapping, including the multi-owner case a
linked/shared file requires. `ChangedInput.OwningProjectIds` already accepts a list, so §2a's
disposition rules are compatible with whatever that resolver eventually produces. Imported/conditional
evaluation failures and the production fallback contract remain unresolved safety boundaries behind
Outcome C.

## Question 3 — Dependency closure

### 3a. Reference-graph dependents closure by shape and position

Closure direction is not uniform across shapes or positions. This first pass measures the
*reference-graph dependents* direction (reverse reachability: who could break if this project
changes) as one candidate scope. `K` growth by scale variable `ΔP` (changed position) and `P` (total
projects), `E` (edges):

| Shape | Change position | K at P=8 | K at P=16 | K at P=32 | K at P=64 | Growth |
|---|---|---:|---:|---:|---:|---|
| Any of the four | leaf (index 0, no dependents) | 1 | 1 | 1 | 1 | Constant, independent of `P` and `E`. |
| WideFanOutFanIn | single spoke | 2 | 2 | 2 | 2 | Constant; `K/P` shrinks as `P` grows. |
| Linear / Dense | middle position (`P/2`) | 5 | 9 | 17 | 33 | Linear in position: `K = index + 1`, `K/P ≈ 0.5` at every scale. |
| Diamond | tail middle position (`P/2`) | 5 | 9 | 17 | 33 | Same linear-tail growth once past the fixed 4-node head. |
| Any of the four | shared foundation (index P−1) | 8 | 16 | 32 | 64 | `K = P` at every scale — full fallback in all four topologies. |

Every cell above is measured at its stated `P`, not extrapolated from a single sample size:
`LeafProjectChange_AffectsOnlyItself` and `SharedFoundationProjectChange_AffectsEveryProject` run at
all four sizes (an earlier revision ran them only at `P=12` and generalized the "at every scale"
claim by hand — that gap is now closed), and the middle-position/spoke rows already looped over all
four sizes from the start.

The headline finding: **`K` does not stay small as a rule.** Whether it does depends entirely on
which project changed, not on solution size. A scope planner cannot assume "changed project ⇒ small
`K`"; it must compute the actual closure per change and report the resulting ratio.

### 3b. Correction — the dependents closure is not one safe superset for every evaluator family

An earlier revision of this document claimed the reference-graph dependents closure from §3a was "the
safe superset for cross-project correctness contracts (layering, cyclic checks, public-API
compatibility)." Reading the actual checker implementations under
`src/ArchLinterNet.Core/Execution/` shows that claim was wrong, and #503's acceptance criteria
explicitly forbid this kind of one-direction assumption. This section has been revised twice more
since: a second review pass found the first correction still understated two families'
requirements (`ReferenceGraphLocal` was claimed narrower than it actually is, and `coverage` was
treated as one family when its six schema-defined scopes split into two shapes); a third review pass
found that second correction had still misclassified `coverage`'s `namespace` scope as project-local
alongside `project`/`assembly`, when a C# namespace is not tied to one assembly the way a project or
assembly output is. The table below is the corrected state, with the `K=9` §3a baseline (Linear,
`P=16`, position index 8) as a fixed comparison point:

| Evaluator family | What the checker actually iterates over | Required scope if only the changed project changes | vs. the §3a dependents closure (`K=9`) |
|---|---|---|---|
| `EvaluatorFamily.ReferenceGraphLocal` (`layers`, `external`/`external_allow_only`, `allow_only`) | Only the changed project's own layer's own outgoing references (`context.FindTypesInLayer(sourceLayer)`, then that layer's own reference/IL scan in `LayerChecker`/`ExternalDependencyChecker`/`AllowOnlyChecker`). But the violation verdict for each reference is decided by `ArchitectureNamespaceViolationFinder.MatchReference`, which classifies the *target* type (namespace/role/expression facts via `ArchitectureLayerTypeMatcher.Matches`), not the source. | The changed project's transitive dependents (`K=9`) — **not** the changed project alone. If unchanged project A references a type in changed project B, and B's change alters that type's own classification, A's already-passing check can flip even though A itself did not change. | **Equal, not narrower.** The first correction claimed `K=1` here; that was itself wrong — this family needs the same bound as the generic default absent a per-type target-fact invalidation model, which this task does not build. |
| `EvaluatorFamily.CyclesGlobal` (`cycles`) | `CycleChecker` builds one shared inter-layer edge graph across every layer named by the contract (`CollectCycleEdgesForLayer` populates one `state.Graph`) and runs global cycle detection once over it (`ArchitectureCycleDetector.FindCycles(state.Graph)`). | Every project whose layer participates in the same cycle contract; this task has no modeled layer-membership graph, so it conservatively falls back to the full population (`K=16`). | **Not a subset relationship at all** — even the dependents closure is *unsafe* here: cycle detection runs over one shared multi-layer graph, not the changed project's own reachability tree. |
| `EvaluatorFamily.ContractCoListing` (`public_api_surface`) | `PublicApiSurfaceChecker` scopes to only the assemblies explicitly named in one contract's `assemblies` list (`ScanContractAssemblies` loops `contract.Assemblies`); a downstream consumer not co-listed in that same contract is unaffected even though it depends on the changed project through the ordinary project-reference graph. | The assemblies co-listed with the changed project in the same contract — a contract-*membership* relationship this task does not model, so it falls back to the full population (`K=16`) rather than assuming the reference graph applies at all. | **Different relationship entirely**, not narrower or wider along the same axis — this is why `ChangedInputKind.ApiSnapshotOrBaselineChange` is `UnmappableFallback` in Question 2, not a dependents-closure expansion. |
| `EvaluatorFamily.AggregatedGlobalScan` (coverage scopes `project`, `assembly`) | `CheckProjectCoverageContract`/`CheckAssemblyCoverageContract` classify each item independently from only that item's own namespaces against policy-declared layers (`IsCoveredByDeclaredLayers`) — never another project's code, and each item is by construction exactly one project or one assembly. | The changed project alone is *correctness-relevant* (`K=1`); today's execution re-scans the whole solution regardless, which is a separate execution-model limitation, not a scope-planning one. | **Strictly narrower** — the one family in this table genuinely bounded below the generic dependents closure. |
| `EvaluatorFamily.CoverageGraphOrCatalogWide` (coverage scopes `namespace`, `dependency_edge`, `semantic_role`, `rule_input`) | `namespace` (`ArchitectureCoverageInventory.Build`) groups `session.TypeIndex.AllTypes()` — the *whole solution's* types — by namespace string and picks one representative type; a namespace is not tied to one assembly, so a shared namespace entry can span types owned by several projects. `dependency_edge` (`ArchitectureDependencyEdgeCoverageService.Check`) evaluates declared layer-name pairs against edges observed across the whole coverage inventory; `semantic_role` (`ArchitectureSemanticCoverageService.BuildSummary`) iterates every type from `TypeIndex.AllTypes()` via the shared role catalog — the same unproven cross-project classification risk as `ReferenceGraphLocal`; `rule_input` operates over policy-level contract ids, not project code. | This task has no graph/catalog model precise enough to bound any of the four below the full population (`K=16`). | **Not a subset relationship** — these four coverage scopes do not share `AggregatedGlobalScan`'s per-item-local shape. The second correction grouped `namespace` with `project`/`assembly` at `K=1`; that was itself wrong, since only `project` and `assembly` are inherently single-project/assembly by construction. |

This is implemented and tested, not asserted: `EvaluatorFamilyScopePlanner`
(`tests/ArchLinterNet.Core.Tests/Benchmarking/EvaluatorFamilyScopePlanner.cs`) computes each family's
required scope from the changed project(s), the reference-graph dependents closure
(`ChangedProjectScopePlanner.DependentsClosure`, exposed publicly for this purpose), and the full
project population, and
`EvaluatorFamilies_RequireDifferentScopesThanTheGenericDependentsClosure` in
`ChangedProjectAdvisoryScopePlanningTests.cs` asserts all five rows above against the same fixed
workload used for the §3a `K=9` baseline.

**Scope of this correction**: `schema/dependencies.arch.schema.json` defines roughly 35 named contract
families (`layers`, `cycles`, `allow_only`, `external`, `assembly_dependency`, `package_dependency`,
`coverage` (itself six sub-scopes), `public_api_surface`, `type_placement`, `metric_budgets`, …); this
task analyzes five representative families (four contract-family groups, with `coverage` split in
two) grounded in their actual checker code. The schema-driven
`EverySchemaContractFamilyReceivesAnExplicitSafeDisposition` test now ensures every strict/audit
family, including the base `dependency` family represented by the schema's `contracts.strict` and
`contracts.audit` arrays, receives either one of those reviewed dispositions or
`UnanalyzedSafeFallback` over the full population. Families outside the representative set are therefore **not** claimed to be safely
bounded by a graph model; safe widening is explicit and tested rather than silently inheriting a
reference-graph direction.

## Question 4 — Canonical scope plan and preview/execution parity

A later implementation's minimum scope-plan contract, informed by this evidence:

- **Plan identity**: bound to a content hash of (repository/build input identity, changed-input set,
  graph/edge snapshot identity) — reusing the existing `analysis-cache/v1` evaluated-input identity
  model rather than inventing a second one, so a stale plan is detectable the same way a stale cache
  entry already is.
- **Per-input decisions**: the ten-row table in Question 2, each carrying kind, disposition, reason,
  direct project ids, and expanded project ids — this task's `ChangedInputDecision` record is a
  direct, working sketch of that shape.
- **Per-evaluator-family scope, not one shared scope**: Question 2's changed-input mapping and §3b's
  evaluator-family requirement are two independent dimensions that must both be represented — a plan
  cannot collapse to one project list. `EvaluatorScope` in this task's evidence code is a working
  sketch of that second dimension: the production contract must record, per evaluator family actually
  present in the policy, that family's own required scope (§3b), not reuse the changed-input closure
  as if it were universal.
- **Directly affected + expanded scope**: the union of all decisions' expanded project ids
  (`ScopePlan.AffectedProjectIds` in this task's planner) — this is the exact required scope for
  `ReferenceGraphLocal` and a safe (wasteful) upper bound for `AggregatedGlobalScan`, but §3b shows it
  is unsafe as the bound for `CyclesGlobal`/`CoverageGraphOrCatalogWide` and undefined (wrong
  relationship) for `ContractCoListing`.
- **Global/unmappable record**: which inputs forced `GlobalExpansion`/`UnmappableFallback` and why,
  so a full-fallback plan is never indistinguishable from a narrow one that happened to compute a
  large closure.
- **One Core authority**: preview and execution must call the same planner function. This task's
  planner already demonstrates the shape is a pure function of `(projects, edges, changed inputs)`
  with no execution-time state, so a read-only preview projection is a non-issue architecturally —
  the harder problem is guaranteeing execution *consumes* the identical plan object rather than
  recomputing it, which is an execution-plumbing concern outside this evidence task's scope.

This task does not ship that contract as a production type; `ChangedInput`/`ChangedInputDecision`/
`ScopePlan` in `ChangedProjectScopePlanner.cs`, and `EvaluatorFamily`/`EvaluatorScope` in
`EvaluatorFamilyScopePlanner.cs`, are test-only evidence, not the reviewed API.

## Question 5 — Coverage accounting

Not implemented here (no execution exists to account for), but the evidence constrains the design:
because `ScopePlan.AffectedProjectIds` is fully determined before any execution starts, an
implementation can and must record, per planned project, one of `Completed` /
`Failed` / `Cancelled` / `Unsupported` / `FallbackExpanded` — mirroring the
`BenchmarkEvidenceDisposition` vocabulary already established in the #502 evidence schema
(`Candidate` / `NotReproduced` / `Deferred` / `Implemented`) rather than a new one. A planned project
that silently has no outcome recorded must be treated as a contract violation of the plan, not a
default success.

## Question 6 — Advisory semantics

Given Question 3's finding that `K = P` is common (shared-foundation and all four global/unmappable
input classes), an advisory "clean" result can only be interpreted as "the planned `K`-project subset
is clean" — it can never be presented as whole-solution cleanliness without a separately proven
equivalence condition, because this evidence shows no static rule guarantees `K < P` for an arbitrary
PR. Full strict validation remains the authoritative result in every case; nothing in this task's
scope-plan model authorizes skipping it.

## Question 7 — Reuse relationship

- **`analysis-cache/v1`** is an exact-request, whole-solution cache: it hits only when the entire
  evaluated input set is byte-identical to a prior run, which is false for almost every PR by
  definition (a PR changes something). It does not help select a `K`-project subset and is not a
  prerequisite for this evidence or a future implementation.
- **`prepared-analysis/v1` (#492)** reuses parsed facts across independent read-only commands within
  *one* revision (breadth of commands), which is a different axis from breadth of *project subset*
  within one command. A scope-planned advisory run could combine with a prepared state once one
  exists, but #492 is not required to make Question 1–6's contract implementable, consistent with
  #503's own instruction not to make #492 a prerequisite absent measured dependency.

Neither reuse candidate is made a prerequisite by this evidence.

## Required pre-implementation effect estimate

- **Baseline full-validation work model**: full strict validation evaluates all `P` projects
  regardless of what changed; this is the existing, unconditional behavior.

- **Affected-scope ratio `K/P` by representative change shape** (from Question 3): leaf ≈ `1/P` → 0
  as `P` grows; single spoke ≈ `2/P` → 0 as `P` grows; middle-position/tail ≈ `0.5` constant; shared
  foundation and all global/unmappable classes = `1.0`.

- **Proposed changed-scope work model**: `advisory_work ≈ fixed_repository_wide_work + (K/P) × per_project_work + scope_planning_overhead`. The `K/P × full_validation_work` shorthand used
  elsewhere in this document is a **simplification that likely overstates the reduction**: it assumes
  work scales uniformly with project count and ignores fixed, repository-wide phases that do not
  shrink with `K` — project/policy discovery, dependency-graph construction, and any
  whole-solution-scoped contract or report phase all run once per invocation regardless of how many
  projects are in scope. Neither this evidence task nor the existing #502/#655 phase-share evidence
  separates `fixed_repository_wide_work` from `per_project_work` for the normalized consumer lane,
  so the synthetic timing result below is evidence about the modeled boundary, not a universal latency
  promise. Scope-planning overhead itself is `O(P + E)` graph traversal and is negligible by comparison
  at every measured size (closure computation for the largest measured graph, `P=64`/`Dense`/full
  density, completes in low single-digit milliseconds; see the test run timings in
  `ChangedProjectAdvisoryScopePlanningTests`).

- **Expected deterministic work reduction, S/M/L**: for the *favorable* subclass (leaf/spoke
  changes), the simplified `K/P` model gives an **upper bound** on reduction that grows with scale —
  at `P=8` a spoke change avoids at most `6/8 = 75%` of project-level work; at `P=64`, at most
  `62/64 ≈ 97%`. These are upper bounds, not measured reductions: they apply only to `per_project_work`
  and do not net out `fixed_repository_wide_work`, which does not shrink with `K` and therefore lowers
  the realizable end-to-end percentage by an amount this task has not measured. For the *unfavorable
  but common* subclass (shared-foundation, and the ~10% of this repository's own PR history touching
  central-props/policy paths per the traffic-mix evidence), reduction is `0%` at every scale under
  either model — full fallback.

- **Mapping/scope-plan/closure overhead**: bounded and cheap (pure graph traversal), evidenced above;
  the dominant cost driver is not the planner, it is how often real PRs land in the unfavorable
  subclass.

- **Best/common/worst case**: best = constant-`K` spoke-shaped change at large `P` (near-100%
  avoidable); common = mixed PR touching a handful of leaf/mid-position projects (partial, shape- and
  position-dependent reduction); worst = any global/unmappable input, which this repository's own
  history shows in roughly 1 of every 10 commits — full fallback, `0%` reduction, by design (safe
  widening, never silently narrowed).

- **Secondary timing/effect evidence**: the explicit harness measures full-strict phase time at S/M/L
  scales and applies an explicitly optimistic K/P upper-bound model. Only `contract_checks` is allowed
  to scale; setup, preflight, post-processing, repository metrics, policy/configuration, and any other
  phase remain unscaled. The model assumes the entire `contract_checks` phase scales linearly, which
  is itself an upper-bound assumption rather than measured incremental behavior. It is not an
  incremental execution benchmark:

  | Scale | Full strict | Unscaled phase residual | `contract_checks` upper-bound phase | Leaf modeled upper bound | Middle modeled upper bound | Global/shared |
  |---|---:|---:|---:|---:|---:|---:|
  | S (8 projects) | 1,166 ms | 1,020 ms | 146 ms | 10.955% | 4.695% | ≈0% |
  | M (16 projects) | 1,221 ms | 1,132 ms | 89 ms | 6.832% | 3.187% | ≈0% |
  | L (32 projects) | 1,232 ms | 1,113 ms | 119 ms | 9.354% | 4.525% | ≈0% |

  These are modeled upper bounds, not measured reductions: all phase time except `contract_checks`
  stays unscaled, while even the eligible phase may contain work that cannot safely be narrowed.
  Planner overhead is 0.017–0.039 ms per measured scale point and is not the limiting factor. The complete raw profiles,
  counters, canonical result identities, and modeled rows are in
  [`changed-project-advisory-analysis-timing-results.json`](changed-project-advisory-analysis-timing-results.json).

- **Decision threshold**: this task sets no percentage cutoff. #502 explicitly requires an
  issue-specific threshold derived from measured hotspot share, workload and implementation/trust
  cost, and #991 has not yet supplied normalized consumer evidence from which to derive that
  threshold. The timing values above are descriptive only. Any future Outcome A/B proposal must derive
  its own success and kill criteria from those inputs.

## Required decision outcome

**Outcome C — unsafe implementation boundary.** The evidence supports closing #503 without creating
an implementation child under #19 because a selective advisory result cannot yet be proven safe.
The ownership fixtures exercise MSBuild evaluated `@(Compile)` items, but there is no production
changed-path-to-owner planner, including its failure and imported/conditional evaluation contract.
Only a small set of evaluator scopes has a reviewed narrower disposition; the base `dependency`
family and other unmodeled contract families safely fall back to the full population. The proposed
narrow coverage scope also is not consumed by a production execution path, and preview/execution
parity plus per-unit coverage accounting are not established. A clean-looking partial result would
therefore have no proven completeness boundary.

The measured timing table is descriptive evidence about the synthetic #502 workloads. It does not
establish an implementation threshold: #502 forbids a universal percentage SLA, and #991 has not yet
provided the normalized consumer measurements needed to derive an issue-specific threshold. The
independent unsafe-boundary findings are sufficient for Outcome C under #503's decision criteria.
Full strict validation remains authoritative; no partial analyzer execution is introduced by this
task.

The decision is therefore:

- close the evidence task as Outcome C;
- do not create a focused implementation issue under #19 from this evidence;
- retain the planner, ownership fixtures, family fallback coverage, and timing artifact as reusable
  evidence for a later re-evaluation after #991;
- reopen only if normalized consumer-shaped measurements show material residual benefit and the
  production ownership, family-specific scope, preview/execution parity, and coverage contracts are
  all proven together.

**Residual scope-model boundary**: only the `AggregatedGlobalScan`-shaped family
(coverage's `project`/`assembly` scopes) is analyzed as narrower than the generic dependents closure
in §3b. `ReferenceGraphLocal` (`layers`/`external`/`allow_only`) turned out, after correction, to need
the *same* dependents-closure bound as the generic default — no better, no worse. `CyclesGlobal`,
`ContractCoListing`, and `CoverageGraphOrCatalogWide` (now including coverage's `namespace` scope) all
fall back to the full population. So the potentially beneficial path is narrow on three axes, not
two: it holds only for changed inputs whose ownership is somehow already known (§2b) and which map
(§2a) to a bounded, non-foundational subset of projects, evaluated only against
`AggregatedGlobalScan`-shaped contract families (§3b) — every other analyzed family, and every
unanalyzed one, means full-validation fallback under the safe-widening principle. Central build/package
props, analyzer/generator/additional files, policy/import files, API-snapshot/baseline changes (per the
Question 2a revision above), and unmappable build-context inputs remain full-validation fallback with
no exception. The Question 4 scope-plan contract and Question 5 coverage model apply to the narrow
case exactly as to the general case: one Core scope authority per dimension (changed-input *and*
evaluator family), explicit per-input disposition, and complete coverage accounting — narrow support
is not permission to ignore inputs or evaluator families outside the analyzed set.

This task does **not** create a focused implementation issue under #19. The evidence leaves three
bounded follow-ups for any future re-evaluation: (1) build and test a production change-to-project
ownership resolver grounded in MSBuild's evaluated `@(Compile)` items (§2b), since the current
ownership tests intentionally stop at the resolver seam; (2) replace conservative fallback with
checker-grounded family rules only where the target policy can prove a narrower scope, including a
real per-type target-fact invalidation model for `ReferenceGraphLocal`/`CoverageGraphOrCatalogWide`'s
`semantic_role` scope; and (3) after #991 reaches its decision gate, compare normalized consumer CI
spans with this synthetic timing boundary. Reopen implementation work only if all three dimensions
show material residual benefit together.

## Routing and non-goals

- No changed-file-only path is presented as a replacement for full strict validation.
- No incremental validation, cache, or invalidation logic is implemented by this task; the
  `ChangedProjectScopePlanner` and `EvaluatorFamilyScopePlanner` in
  `tests/ArchLinterNet.Core.Tests/Benchmarking/` are test-only evidence infrastructure, not a product
  capability, and are excluded from the reviewed public API the same way the rest of the #502
  Benchmarking folder is.
  No implementation child is created from this evidence; #991 is the authority for any later
  re-evaluation. See
  [P0 consumer-normalization gate](#p0-consumer-normalization-gate-991).
- No single reference-graph direction is presented as sufficient for every contract family; §3b's
  five-family analysis is representative, not exhaustive, and unanalyzed families default to full
  fallback rather than an assumed bound.
- No path-based ownership heuristic is presented as the authoritative change-to-project mapping; §2b
  is explicit that this task supplies only the disposition step given already-known ownership, not a
  tested `path → owning project(s)` resolver.
- `analysis-cache/v1` and `prepared-analysis/v1` (#492) reuse relationships are evidence-based, not
  assumed prerequisites — see Question 7.
- No private adopter identity, repository URL, or proprietary topology is committed; all closure
  evidence uses the #502 `Synthetic.ProjectNNN` identities, and the PR-traffic-mix evidence uses only
  this public repository's own commit history.

OpenSpec: not applicable. This task adds internal test-only evidence infrastructure and a design
document only; it changes no public API, policy semantics, cache trust boundary, or documented user
guarantee, and non-goals explicitly exclude implementing incremental validation in this issue.
