# Design

## Context

`ArchitectureAnalysisSession` is the per-run state object: it builds the type/role/source-fact indexes once, holds the document and contract selection, and accumulates unmatched-ignore, baseline-candidate and finding-identity-candidate state as contracts execute. Before this change it was also where most contract families' checking algorithms were written — 30 partial files, roughly 4,000 lines of family behavior, with `ArchitectureAnalysisSession.Checking.cs` alone carrying ten families.

The constraint that shapes every decision below: this is behavior-preserving. Findings, canonical identities, ordering, lifecycle status, cache behavior and cancellation semantics must be structurally equivalent for unchanged inputs.

## Goals / Non-Goals

**Goals**

- Family checking has a focused home per family, reached through the existing registry/descriptor dispatch.
- The session's remaining responsibilities are legible: lifecycle, immutable/request-scoped state, fact/index access, cache-facing state, deterministic coordination.
- Canonical finding-identity attribution is testable without a session.
- Self-policy makes reintroduction of session-owned family checking fail a test rather than pass review.

**Non-Goals**

- Parallel contract-family execution (#19). Execution stays sequential and authoritative.
- Replacing `analysis-cache/v1` or bounded deterministic fact scanning.
- Redesigning baseline identity, contract semantics, or any public API.
- Renaming or folder cleanup beyond what the extraction needs.

## Decision 1: A narrow checker context, not the session

Checkers receive `ArchitectureCheckerContext`, an internal forwarding facade over the session, rather than the session itself.

Alternatives considered:

- **Pass the session.** Rejected: it is exactly today's coupling with a new file layout. Nothing would stop a new family from reaching lifecycle state, and the self-policy rule would have nothing to assert.
- **Pass each family only the primitives it needs** (the shape the original three extracted checkers use — target assemblies, a resolved assembly lookup, the type index). Rejected as the general rule: families like `layout_conventions` and `port_boundary` need six to eight collaborators, several of which are themselves session-computed (`FindTypesInLayer`, `ResolveContainingLayer`, `ResolveProjectAssemblyNames`, `FindContextSelectorMatchingTypes`), and threading them individually produces long, unstable parameter lists that change every time a family's inputs change. A single named port keeps the boundary auditable and stable. Families that genuinely need only one or two primitives still take them directly — `AssemblyIndependenceChecker` and `InheritanceChecker` are unchanged.

The context forwards; it stores nothing. Every member is a property or one-line delegation, so ordering, caching and mutation semantics are identical to the pre-extraction call sites. Members exist only where a checker demonstrably needs them; contract selection, execution-context creation, unmatched-ignore collection, coverage, policy consistency and cache-facing state are deliberately absent.

## Decision 2: Mutable run state stays session-owned, written through recording ports

Two families append to session-owned lists while checking: `type_placement` and `layout_conventions` record subtractive-matcher participation, and `cycle` publishes baseline candidates for edges that turn out to participate in a cycle.

- **Participation** is recorded through `ArchitectureCheckerContext.RecordSubtractiveMatcherParticipation`. The list stays on the session, so record order remains purely a function of contract-family execution order.
- **Cycle baseline candidates** are *not* published from the checker. `CycleChecker` returns the detected cycles together with the graph and the observed candidate evidence; the session wrapper calls `AddCycleBaselineCandidates` once the cycle set is known. Publication of baseline state stays entirely inside the session, which is the stricter and more honest boundary.

## Decision 3: One deliberate result-shape exception per ordering/lifecycle quirk

Three families do not fit the plain `Check(contract, context, executionContext) -> violations` shape, and each returns a small result record instead of having its quirk smoothed away:

- `LayerChecker.Result` separates exhaustive-sibling findings, because they were appended *after* unmatched-ignore collection in the pre-extraction code. Merging them would reorder findings.
- `CycleChecker.Result` carries the graph and candidate evidence, per Decision 2.
- `LayoutConventionChecker.Result` carries `EvaluatedIgnores`. The whole-run "no source-enriched facts" path previously returned before an execution context existed at all, so it never reported the contract's `ignored_violations` as unmatched. Always collecting would have introduced spurious unmatched-ignore findings for that path.

Each of these is a behavior-preservation record, not a convenience.

## Decision 4: Identity attribution as a pure function

`ArchitectureFindingIdentityAttributor.Attach(candidateLog, cursor, violations)` reads the session's candidate log and returns attributed violations. It writes nothing and holds nothing between calls.

Cancellation is unchanged because the call site is unchanged: `ArchitectureContractExecutor` checks the cancellation token before each contract, then runs that contract's checking to completion and attributes its findings in one call. A cancelled run therefore has either a contract's fully attributed findings or none of them — a partially attributed set is not reachable, exactly as before.

The cursor stays on the session (`FindingIdentityCursor`) because it is a read of session-owned mutable state; only the algorithm moved.

## Decision 5: Coverage stays session-owned, explicitly

`CheckCoverageContract` is not extracted, and the self-policy test lists it as the single allowed session-owned entry point with the reason inline. Coverage is not contract-family checking over declared types; it is a policy-inventory report over the whole document, sharing the session's cached inventory with `CheckConfiguration`. Issue #452 scopes coverage and policy-consistency out. This is a stated boundary, not a silent omission — and because the allowlist is a single named entry with a written justification, a second entry cannot be added without a reviewer seeing it.

## Decision 6: Self-policy as source-level tests

The boundary is about *where code is written*, which compiled metadata cannot express: a fat session method and a thin one have identical signatures. `ArchitectureAnalysisSessionCheckerOwnershipTests` therefore parses the session partials, the checker sources and the family registry with Roslyn (already a Core dependency) and asserts:

1. every public `Check*Contract` entry point contains at most ten statements, counting **every nested statement**, and delegates to a `*Checker` component;
2. an entry point calls no session-declared method outside a named lifecycle/fact-access allowlist;
3. the family registry dispatches only into methods matching the `Check*Contract` entry-point shape;
4. no file under `Execution/Checkers` names `ArchitectureAnalysisSession`;
5. finding-identity attribution stays in `ArchitectureFindingIdentityAttributor`, with the session method a delegation.

Rules 1–3 are three distinct locks, and the PR #580 review is why: a top-level-only statement count passes a method whose whole algorithm sits inside one `if` (rule 1's nested counting closes that); a size bound of any kind passes an entry point that stays small by calling a new session-private helper holding the algorithm (rule 2 closes that by making such a helper unreachable from the family's execution path); and both are moot for a family whose descriptor dispatches into a session method these rules never inspect (rule 3 closes that). Each was verified by injecting the bypass and observing the corresponding rule fail.

Rules 2 and 4 forced two small moves: `NormalizeProjectPath` became `ProjectPathNormalizer.Normalize` under `Discovery`, and `BuildSurfaceSelectorPredicate` — public-api-surface selector construction that had been sitting on the session since #529 — moved onto `PublicApiSurfaceChecker`, with the session's capture path calling into the checker for it. Neither should have been reachable from the session for a family's own behavior.

## Risks / Trade-offs

- **Mechanical-move risk.** ~4,000 lines moved. Mitigation: the existing suites are the equivalence oracle — the Core public-API approval test pins the public surface byte-for-byte, and the identity, baseline, cache-equivalence, sequential-vs-parallel-scanning and cancellation suites all run unchanged.
- **The context could grow into a god object.** It is internal, every member is justified by a concrete caller, and the two self-policy rules make additions visible. If a future family needs something genuinely lifecycle-shaped, that is a signal to reconsider the boundary rather than to widen the port quietly.
- **A statement-count bound is a proxy.** Ten statements is loose enough for the wrappers that also publish candidates or append a separately-ordered finding set, and far too tight for any real family algorithm. It fails loudly and its message names the fix.
