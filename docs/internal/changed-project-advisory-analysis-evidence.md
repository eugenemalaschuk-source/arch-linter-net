# Changed-project advisory analysis and dependency-aware invalidation evidence (#503)

## Decision

This is an evidence-first measurement and design gate. No changed-file-only validation path, scope
planner, or invalidation cache is implemented by this issue, and no output from this task weakens
full strict validation as the authoritative result.

The checked-in machine-readable artifact is
[`changed-project-advisory-analysis-results.json`](changed-project-advisory-analysis-results.json).
Unlike the wall-clock benchmark evidence for #493/#655/#461, the closure numbers here are **exact
deterministic graph computations**, not timed samples: every row is reproduced by an assertion in
[`ChangedProjectAdvisoryScopePlanningTests.cs`](../../tests/ArchLinterNet.Core.Tests/ChangedProjectAdvisoryScopePlanningTests.cs),
which runs in every normal `make test` pass (no `[Explicit]` exclusion — there is no hardware
sensitivity to isolate).

**Outcome: B, narrow and material for specific change classes, with implementation-child creation
deferred by the #991 P0 gate.** See [Required decision outcome](#required-decision-outcome).

## P0 consumer-normalization gate (#991)

Per #503's own routing note, this evidence/design task may proceed while consumer CI is normalized,
but no incremental-analysis implementation child may start solely from the pre-normalization dogfood
latency table recorded in #991 (`arch-linter-net` self-CI 204s, `firstice-server` 1073s, `firstice`
Unity 41s, `firstice-map-editor` 249s). This task therefore:

- records the deterministic closure evidence and scope-plan contract now, since that evidence is
  independent of consumer CI topology;
- does **not** create a focused implementation issue under #19 in this task, even though the
  evidence below shows narrow cases are materially favorable, because the required
  pre-implementation effect estimate cannot yet isolate residual product-internal latency from
  consumer-orchestration overhead;
- records the exact re-evaluation trigger: after #991 reaches its decision gate, recompute the
  expected end-to-end effect against normalized dogfood spans before deciding whether to open the
  child issue.

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
- Separately, classify the last 300 commits on this repository's own `main` history by file path to
  get a real (if approximate) PR-traffic-mix prior, complementing the synthetic closure math with
  evidence about how often this repository's own changes touch globally-scoped inputs.

## Environment

Closure measurements are exact graph computations with no environment sensitivity: the same input
graph always produces the same closure on any machine, .NET version, or OS. No wall-clock,
processor-time, or allocation sampling applies to this evidence, unlike the #493/#655/#461 harnesses.
The repository-history classification was run against this repository's `main` branch at commit
`bb533f0f` (2026-09-22).

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

Repository-history evidence (last 300 commits, path-pattern classification, see
[`changed-project-advisory-analysis-results.json`](changed-project-advisory-analysis-results.json)):
only 10.3% of commits touch a central-build-props or policy path that would force global fallback
under these rules; 5.0% touch only project source/`.csproj` paths (a clean scoped case). The
remaining ~85% mix project source with docs/OpenSpec/other paths whose architecture-lint relevance
this task does not resolve — a correct implementation would need to classify those as excluded
(no re-analysis needed) or direct-mapped, not silently folded into "global." That mapping question
is exactly what a focused implementation issue would need to answer with real path-ownership rules,
not path-pattern heuristics.

**Conclusion**: material user-outcome benefit exists, but only for a bounded subclass of PR-shaped
changes (leaf/spoke-shaped, low-dependent-count changes); it is not a general "PR feedback gets
faster" claim.

## Question 2 — Change-to-project mapping

`ChangedProjectScopePlanner` gives every changed-input kind from #503's own list one explicit,
tested disposition. No kind is silently dropped:

| Changed input kind | Disposition | Reason |
|---|---|---|
| Project-owned source file | `DependencyDrivenExpansion` | Owned by exactly one project; scope widens to that project's transitive dependents. |
| Shared/linked source file (2+ owning projects) | `DependencyDrivenExpansion` | Scope starts from every owning project and widens to their combined dependents. |
| Project-reference edge change (`.csproj` reference add/remove) | `DependencyDrivenExpansion` | Both endpoints are seeded; an edge's presence/absence can change graph-shaped contract results for either side. |
| `.csproj` property edit (non-reference) | `DependencyDrivenExpansion` | Same rule as an owned source file. |
| Package/framework reference change (one project) | `DependencyDrivenExpansion` | Same rule as an owned source file. |
| `Directory.Build.*` / `Directory.Packages.props` / `.editorconfig` / `NuGet.config` / `global.json` | `GlobalExpansion` | Applies to every project; no static mapping can bound the set below the full population. |
| Analyzer/generator/additional file | `GlobalExpansion` | Can change compiled output for any consuming project in ways static reference analysis cannot verify. |
| Policy/import file (`architecture/*.yml`) | `GlobalExpansion` | Can change selector membership, layer boundaries, or contract scope for any project. |
| Baseline / reviewed public-API snapshot (`architecture/api/*.public-api.txt`) | `DependencyDrivenExpansion` | Documents exactly one package's public surface; scoped to that project's dependents, not global — see caveat below. |
| Generated output / build-context change | `UnmappableFallback` | No reviewed static ownership mapping exists; the input is unmappable and falls back to full scope rather than being excluded. |

Caveat on the API-snapshot row: this evidence task treats it as project-scoped because each
`*.public-api.txt` file documents one package's surface (Core, Testing, or CEL). A future
implementation must confirm that no contract family treats the reviewed snapshot as a cross-project
or repository-wide input before relying on this narrower disposition; if any does, that snapshot kind
must move to `GlobalExpansion` like policy/import changes.

Every measured decision carries a human-readable reason string (asserted by
`GlobalOrUnmappableInputs_WidenToTheFullProjectPopulation` and
`EveryChangedInput_ReceivesExactlyOneDeterministicDecision`); none of the ten kinds above returns an
implicit "no-op" or silently excludes an input.

## Question 3 — Dependency closure

Closure direction is not uniform across shapes or positions — this evidence measures the *dependents*
direction (reverse reachability: who could break if this project changes), which is the safe
superset for cross-project correctness contracts (layering, cyclic checks, public-API compatibility).
A project-local contract (e.g. an external-dependency allow-list scoped to one project) would only
need the changed project itself — a strict subset of the dependents closure — which confirms the
issue's own instruction that "a single graph direction is [not] sufficient for all contracts": the
dependents closure is a safe default, not a universal minimum.

`K` growth by scale variable `ΔP` (changed position) and `P` (total projects), `E` (edges):

| Shape | Change position | K at P=8 | K at P=16 | K at P=32 | K at P=64 | Growth |
|---|---|---:|---:|---:|---:|---|
| Any of the four | leaf (index 0, no dependents) | 1 | 1 | 1 | 1 | Constant, independent of `P` and `E`. |
| WideFanOutFanIn | single spoke | 2 | 2 | 2 | 2 | Constant; `K/P` shrinks as `P` grows. |
| Linear / Dense | middle position (`P/2`) | 5 | 9 | 17 | 33 | Linear in position: `K = index + 1`, `K/P ≈ 0.5` at every scale. |
| Diamond | tail middle position (`P/2`) | 5 | 9 | 17 | 33 | Same linear-tail growth once past the fixed 4-node head. |
| Any of the four | shared foundation (index P−1) | 8 | 16 | 32 | 64 | `K = P` at every scale — full fallback in all four topologies. |

The headline finding: **`K` does not stay small as a rule.** Whether it does depends entirely on
which project changed, not on solution size. A scope planner cannot assume "changed project ⇒ small
`K`"; it must compute the actual closure per change and report the resulting ratio, which is exactly
what the scope-plan contract in Question 4 requires.

## Question 4 — Canonical scope plan and preview/execution parity

A later implementation's minimum scope-plan contract, informed by this evidence:

- **Plan identity**: bound to a content hash of (repository/build input identity, changed-input set,
  graph/edge snapshot identity) — reusing the existing `analysis-cache/v1` evaluated-input identity
  model rather than inventing a second one, so a stale plan is detectable the same way a stale cache
  entry already is.
- **Per-input decisions**: the ten-row table in Question 2, each carrying kind, disposition, reason,
  direct project ids, and expanded project ids — this task's `ChangedInputDecision` record is a
  direct, working sketch of that shape.
- **Directly affected + expanded scope**: the union of all decisions' expanded project ids
  (`ScopePlan.AffectedProjectIds` in this task's planner).
- **Global/unmappable record**: which inputs forced `GlobalExpansion`/`UnmappableFallback` and why,
  so a full-fallback plan is never indistinguishable from a narrow one that happened to compute a
  large closure.
- **One Core authority**: preview and execution must call the same planner function. This task's
  planner already demonstrates the shape is a pure function of `(projects, edges, changed inputs)`
  with no execution-time state, so a read-only preview projection is a non-issue architecturally —
  the harder problem is guaranteeing execution *consumes* the identical plan object rather than
  recomputing it, which is an execution-plumbing concern outside this evidence task's scope.

This task does not ship that contract as a production type; `ChangedInput`/`ChangedInputDecision`/
`ScopePlan` in `ChangedProjectScopePlanner.cs` are test-only evidence, not the reviewed API.

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
- **Proposed changed-scope work model**: `advisory_work ≈ (K/P) × full_validation_work + scope_planning_overhead`. Scope-planning overhead is `O(P + E)` graph traversal — negligible next
  to project-level compilation/analysis work at every measured size (closure computation for the
  largest measured graph, `P=64`/`Dense`/full density, completes in low single-digit milliseconds;
  see the test run timings in `ChangedProjectAdvisoryScopePlanningTests`).
- **Expected deterministic work reduction, S/M/L**: for the *favorable* subclass (leaf/spoke
  changes), reduction grows with scale — at `P=8` a spoke change already avoids `6/8 = 75%` of
  project-level work; at `P=64` it avoids `62/64 ≈ 97%`. For the *unfavorable but common* subclass
  (shared-foundation, and the ~10% of this repository's own PR history touching central-props/policy
  paths per the traffic-mix evidence), reduction is `0%` at every scale — full fallback.
- **Mapping/scope-plan/closure overhead**: bounded and cheap (pure graph traversal), evidenced above;
  the dominant cost driver is not the planner, it is how often real PRs land in the unfavorable
  subclass.
- **Best/common/worst case**: best = constant-`K` spoke-shaped change at large `P` (near-100%
  avoidable); common = mixed PR touching a handful of leaf/mid-position projects (partial, shape- and
  position-dependent reduction); worst = any global/unmappable input, which this repository's own
  history shows in roughly 1 of every 10 commits — full fallback, `0%` reduction, by design (safe
  widening, never silently narrowed).
- **Expected end-to-end effect**: cannot be stated as a single number without conflating
  product-internal latency with consumer-CI orchestration overhead — this is exactly the distinction
  the #991 P0 gate requires be resolved first. The `K/P` ratios above are consumer-independent and
  usable immediately once #991 supplies normalized baseline spans; this task does not fabricate an
  end-to-end percentage against the pre-normalization dogfood table.
- **Success threshold / kill criterion (per #991 re-evaluation)**: propose, for the next
  re-evaluation: success = a supported change class (leaf/spoke-shaped) demonstrates ≥50% reduction
  in normalized product-internal governance span with proven preview/execution parity and zero
  coverage gaps across the #502 fixture matrix; kill = the real PR-traffic mix (once measurable from
  normalized consumer history rather than this repository's own commits) shows global/unmappable
  inputs dominate enough that the weighted-average reduction falls below a low single-digit percent.

## Required decision outcome

**B — material only for narrower cases, implementation-child creation deferred by the #991 P0 gate.**

Narrow supported change classes: a changed input that maps (directly or via dependency expansion) to
a bounded, non-foundational subset of projects — concretely, source/property/package/API-snapshot
changes to projects whose dependents closure does not degenerate to the full population. Central
build/package props, analyzer/generator/additional files, policy/import files, and unmappable
build-context inputs remain full-validation fallback with no exception. The Question 4 scope-plan
contract and Question 5 coverage model apply to the narrow case exactly as to the general case: one
Core scope authority, explicit per-input disposition, and complete coverage accounting — narrow
support is not permission to ignore inputs outside the supported class.

This task does **not** create a focused implementation issue under #19. Per the #503 P0 gate, doing
so now would rely on the same pre-normalization dogfood latency #991 explicitly disqualifies as sole
justification, and this evidence's own effect model cannot yet separate residual product-internal
latency from consumer-orchestration overhead. The required next action is recorded here: after #991
reaches its decision gate, re-run the Question 1/effect-estimate reasoning above against normalized
consumer CI spans, and open the implementation issue only if a supported change class still shows
material residual benefit.

## Routing and non-goals

- No changed-file-only path is presented as a replacement for full strict validation.
- No incremental validation, cache, or invalidation logic is implemented by this task; the
  `ChangedProjectScopePlanner` in `tests/ArchLinterNet.Core.Tests/Benchmarking/` is test-only
  evidence infrastructure, not a product capability, and is excluded from the reviewed public API the
  same way the rest of the #502 Benchmarking folder is.
  Implementation-child creation is deferred pending #991; see
  [P0 consumer-normalization gate](#p0-consumer-normalization-gate-991).
- `analysis-cache/v1` and `prepared-analysis/v1` (#492) reuse relationships are evidence-based, not
  assumed prerequisites — see Question 7.
- No private adopter identity, repository URL, or proprietary topology is committed; all closure
  evidence uses the #502 `Synthetic.ProjectNNN` identities, and the PR-traffic-mix evidence uses only
  this public repository's own commit history.

OpenSpec: not applicable. This task adds internal test-only evidence infrastructure and a design
document only; it changes no public API, policy semantics, cache trust boundary, or documented user
guarantee, and non-goals explicitly exclude implementing incremental validation in this issue.
