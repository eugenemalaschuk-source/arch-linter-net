# Release Lifecycle and Milestone Governance

This is the canonical internal process contract for how ArchLinterNet plans, delivers, releases, stabilizes, and closes a product milestone.

It explains **why** release milestones contain both product work and post-release engineering work, how minor and patch releases relate to that milestone, and which work may be deliberately deferred without turning technical debt into an invisible or unlimited budget.

This document is internal repository-maintenance documentation. It must not be added to the public MkDocs navigation or used as NuGet-facing product documentation.

## Authority and related documents

This document owns the **release lifecycle and milestone semantics**.

Other documents own narrower mechanics:

- `docs/internal/backlog-governance.md` — issue hierarchy, issue structure, and backlog authoring rules;
- `docs/ai/release-preparation-workflow.md` — repository-side preparation of a concrete release candidate and reviewed release scope;
- `docs/reference/release-process.md` — maintainer publication mechanics, package/provenance authority, dry-run, and public publication;
- `AGENTS.md` — repository-agent routing and required entry points.

A milestone, issue checklist, or this process document never replaces immutable release-scope and packed-candidate evidence for a concrete publication.

## Why this process exists

The process is based on repeated release experience rather than a theoretical release model.

### v0.6 — post-release architecture pressure is real

The v0.6 capability wave added enough new behavior that the architecture pressure became easier to understand **after the release boundary existed**. The post-release architecture audit under #450 identified concrete hotspots and split them into independent behavior-preserving refactoring lanes.

Lesson: deep structural cleanup is often better scoped from a stable release delta than mixed into every feature PR.

### v0.7 — internal completion is not the same as real adoption

v0.7.0 was internally implementation-complete and publishable, but installing the released product in a real server-shaped consumer exposed correctness and integration defects that prevented the intended workflow from being adopted cleanly. Several maintenance releases were required before the 0.7 capability line was practically stable.

The 0.7 line therefore distinguishes two facts that must never be conflated:

- **the product increment was implemented and released**;
- **the released increment was proven usable in a real consumer**.

The 0.7 dogfood/technical-improvement work under #630 and the accumulated Sonar cleanup under #632 happened alongside that functional stabilization.

Lesson: a minor tag does not prove adoption completeness. A real released-artifact consumer loop is a separate acceptance layer.

### v0.8 — make the lessons explicit

v0.8 moved more of the consumer proof before publication through packed, consumer-shaped Checkpoint B scenarios and final composition acceptance. At the same time #742 deliberately froze known architecture debt instead of pulling a broad god-class refactor into the functional critical path.

After v0.8.0, the cleanup sequence became explicit:

```text
release-specific architecture debt (#772)
        -> remaining self-architecture health (#784)
        -> whole-repository SonarCloud debt sweep (#783)
```

Lesson: feature throughput and engineering health can be separated safely only when debt is visible, bounded, ratcheted, and paid down through an explicit post-release phase.

## The release-train model

The default release train is:

```text
1. Plan the milestone as parallel capability lanes
        ↓
2. Implement the product increment with bounded visible debt
        ↓
3. Converge integration + user documentation + consumer-shaped acceptance
        ↓
4. Publish X.Y.0
        ↓
5. Validate the released artifact in real adoption
        ↓
6. Fix adoption/correctness defects through X.Y.patch releases as needed
        ↓
7. Audit and refactor release-created/materially-amplified architecture debt
        ↓
8. Dogfood the released governance against ArchLinterNet itself
        ↓
9. Sweep residual Sonar/reliability/maintainability debt on the post-refactor tree
        ↓
10. Publish the final maintenance patch when the accumulated stabilization delta warrants it
        ↓
11. Close the milestone when the wave is product-stable and engineering-health work is explicitly resolved
```

This is a **maintenance train**, not a rule that every minor must have exactly one patch release. A line may need zero, one, or several patch releases depending on adoption defects and stabilization changes.

## Four different completion states

Agents and maintainers must use precise language when discussing progress.

### 1. Implementation complete

The planned product capability is implemented and its owning issue/story acceptance is satisfied on the development tree.

This does not imply that the public release exists.

### 2. Release complete

The reviewed release candidate has passed its publication authority and `X.Y.0` is published and post-publication verification succeeds.

This does not imply that a real adopter has exercised every important workflow successfully.

### 3. Adoption stable

A real consumer using the **released artifacts** can execute the intended supported workflow without an unresolved correctness/integration blocker attributable to the new release line.

Consumer-shaped pre-release acceptance reduces the risk of post-release defects but does not eliminate the need for this state.

### 4. Milestone / engineering-health complete

The milestone's post-release architecture and quality debt has been handled explicitly:

- release-specific architecture findings are resolved or deliberately assigned;
- self-architecture debt is resolved or explicitly owned;
- residual Sonar/reliability/security/maintainability debt has been swept after the structural tree settles;
- no anonymous stabilization debt remains hidden in comments or dashboards;
- any remaining work is moved to a deliberate future owner rather than implicitly forgotten.

A milestone may therefore remain open after the `X.Y.0` tag exists. This is intentional.

## What a milestone means

A release milestone is the **development-wave envelope**, not the immutable publication manifest.

It may contain three kinds of work at the same time.

### A. Release-required product scope

Capabilities, integration, documentation, correctness, and release-gate work that must be complete for `X.Y.0`.

These become publication blockers only through the reviewed release authority for the concrete candidate.

### B. Explicitly non-blocking release hygiene

Useful work associated with the release line that is deliberately allowed to remain unfinished at the minor cut, for example a low-priority UX cleanup or an optional adapter.

Non-blocking status must be explicit. Milestone membership alone never proves that an item blocks publication.

### C. Post-release stabilization work

Architecture refactoring, self-dogfood cleanup, accumulated quality-debt reduction, and adoption fixes attributable to the just-shipped wave.

Keeping this work in the same milestone preserves causality: the milestone answers not only "what capability did we ship?" but also "what engineering pressure did that capability wave create and how did we stabilize it?"

## Milestone membership is not release authority

This rule is mandatory:

> **A GitHub milestone is planning and traceability metadata. It is never the authority that decides whether an immutable candidate may publish.**

A concrete release is authorized by the repository's release-scope declaration and packed-candidate evidence selected for the candidate version.

Therefore:

- an open post-release cleanup issue in the milestone does not automatically block `X.Y.0`;
- a closed issue in the milestone does not automatically belong to the candidate if its bytes are not in the candidate;
- an issue outside the milestone can still be a release blocker if the reviewed release authority explicitly requires it;
- mutable issue text cannot override the immutable candidate and reviewed release-scope evidence.

## Phase 1 — plan the capability wave for parallel execution

Before implementation begins, shape the milestone into independently executable lanes wherever the product semantics permit it.

Prefer:

```text
shared foundation
   -> lane A
   -> lane B
   -> lane C
          ↓
     convergence / composition
          ↓
       docs / final acceptance
```

Backlog planning should make dependencies explicit rather than serialize work through one oversized umbrella implementation issue.

The planning goal is not the maximum number of issues. It is the maximum amount of **real independent ownership** with clear convergence points.

Each release-level plan should identify:

- product outcome;
- independent capability lanes;
- shared prerequisites;
- convergence/integration authority;
- documentation closure;
- consumer-shaped acceptance;
- publication story/authority;
- known non-blocking work.

## Phase 2 — implement capabilities without mixing in broad cleanup

Feature implementation optimizes for delivering a coherent product increment.

Do not turn every feature PR into a general cleanup of every historical hotspot it touches.

### What may be deliberately deferred

Behavior-preserving structural cleanup may be deferred when all of the following are true:

1. the current structure still preserves correctness and public contracts;
2. the debt is visible and reviewable;
3. the release has a ratchet or equivalent guard preventing unlimited worsening where practical;
4. the debt has a concrete post-release owner or can be reconstructed deterministically from the release delta;
5. deferral materially improves capability throughput or reviewability.

Examples include responsibility extraction from a large but still correct aggregate, formatter decomposition, test-harness decomposition, or replacement of an accepted partial-type debt baseline.

### What must not be deliberately deferred as "cleanup"

The following are not an acceptable technical-debt budget:

- known correctness defects in the promised workflow;
- security defects;
- release-integrity/provenance defects;
- data-loss/corruption risks;
- deterministic false PASS/false success paths;
- unbounded process/resource failures that make release acceptance unreliable;
- a public contract that is known not to work as documented.

Those are defects or release blockers and must be handled by the owning functional/release path.

## Technical-debt ratchet rule

A capability wave may carry known debt; it must not normalize uncontrolled debt growth.

Preferred behavior is:

```text
reviewed known debt baseline
        +
new feature work
        ↓
existing debt unchanged/reduced -> allowed according to release policy
new/increased governed debt      -> visible and blocking unless explicitly reviewed
```

Do not "solve" a ratchet by refreshing the baseline upward after every feature.

When a release intentionally leaves broad cleanup for later, agents should prefer purpose-named collaborators for new code rather than extending the known hotspot further.

## Phase 3 — converge product behavior, docs, and consumer-shaped acceptance

Before the minor cut, converge the independent lanes into the supported user workflow.

The release must synchronize **evergreen user documentation with implemented behavior**. Documentation closure belongs after the public CLI/API/schema semantics are stable enough to describe truthfully and before the final release composition proof.

Consumer-shaped acceptance should use freshly packed candidate artifacts where practical and must exercise the real public boundary instead of source-tree-only shortcuts.

The goal is to detect the class of issue that v0.7 exposed only after publication:

- package/install behavior;
- real CLI invocation composition;
- external-consumer project shapes;
- platform-specific package/runtime behavior;
- command combinations that unit tests do not naturally compose;
- documentation examples that do not actually execute.

Consumer-shaped acceptance is a risk reducer, not a claim that real post-release adoption can never find another defect.

## Phase 4 — publish the minor release

`X.Y.0` is the **capability release**.

Its job is to ship the reviewed user-visible product increment once required product semantics, documentation, integration, candidate evidence, and release authority are complete.

Do not require a broad behavior-preserving refactor merely because the new capability made an existing hotspot more obvious, unless that hotspot itself makes the release unsafe or unacceptably unreliable.

Conversely, do not call a feature "post-release cleanup" when it is actually required to make the promised product work.

## Phase 5 — real adoption validation

After publication, validate the actual released packages/tool in one or more real consumer-shaped repositories or workflows.

The important boundary is:

```text
released NuGet/tool artifact
        -> real consumer configuration
        -> supported end-to-end workflow
```

Do not substitute the source tree or an unpublished local build when the purpose is to prove real adoption.

If adoption reveals a blocker:

1. classify it as a focused product/integration defect;
2. fix it without hiding unrelated cleanup in the same change;
3. publish a patch release when the fix must reach consumers;
4. repeat until the release line is adoption-stable.

This may require multiple patch releases. The 0.7 line is the explicit precedent.

## Phase 6 — post-release architecture audit

Once a stable release boundary exists, inspect the exact release delta for architecture pressure.

Use an explicit range:

```text
previous stable release .. current minor release
```

The audit must separate:

1. debt created by the release;
2. pre-existing debt materially amplified by the release;
3. older debt merely touched by the release;
4. style/preferences without architecture evidence.

Only the first two are automatically release-specific cleanup candidates. Older debt may be included only when a coherent responsibility extraction requires it or when a broader self-architecture pass owns it explicitly.

Create focused, independently reviewable refactoring tasks. Preserve public semantics unless a separately tracked defect owns behavior change.

## Phase 7 — self-architecture stabilization and dogfood

After release-specific hotspots are handled, run the released ArchLinterNet governance capabilities against ArchLinterNet itself.

Use canonical product evidence rather than intuition where the product already has an authority for the concern.

Classify every remaining self-architecture item as one of:

- actionable debt;
- already owned elsewhere;
- intentional architecture represented correctly/incorrectly;
- insufficient product evidence requiring a separate capability decision;
- obsolete debt record that should be removed.

Do not leave a generic anonymous "legacy debt" bucket.

The preferred result is a truthful clean self-governance baseline, not a green badge produced by weakening policy.

## Phase 8 — whole-repository quality cleanup comes last

The authoritative whole-repository Sonar/reliability/maintainability sweep runs **after structure-changing architecture cleanup**.

Reason:

- otherwise time is spent fixing smells in code that is about to be removed or decomposed;
- architecture ownership should be decided by architecture evidence, not by whichever Sonar rule happens to fire first;
- post-refactor Sonar results represent the tree that future feature work will actually inherit.

Priority remains:

1. correctness/security/reliability findings;
2. high-value complexity and duplication;
3. safe behavior-preserving maintainability cleanup.

Do not use broad `NOSONAR`, exclusions, rule disabling, or weaker quality gates to manufacture a clean baseline.

## Phase 9 — maintenance patch train

Patch releases after `X.Y.0` are the distribution mechanism for the stabilization delta.

A patch may contain:

- real-adoption correctness/integration fixes;
- narrowly scoped release/reliability fixes;
- behavior-preserving architecture refactoring;
- behavior-preserving Sonar/maintainability cleanup;
- documentation corrections required by those fixes.

A patch should not contain unrelated next-minor product capability merely because `main` already moved on.

There is no target number of patches. Publish a patch when the accumulated maintenance delta is worth distributing or a consumer fix requires it.

## Protect the maintenance line from next-minor contamination

Planning the next milestone may overlap the previous line's stabilization. **Merging unrelated next-minor product bytes is a different decision.**

Default rule while the current release line still expects maintenance publications:

- next-minor backlog design, issue decomposition, OpenSpec proposals, and non-code planning may proceed in parallel;
- maintenance/refactoring work for the current line may proceed;
- do not merge unrelated next-minor product capability into the only candidate branch/ref if doing so would make a truthful patch release impossible.

If the project deliberately wants next-minor implementation to overlap an open maintenance train, first establish an explicit reviewed maintenance-branch/ref strategy. Do not attempt to use a narrow release-scope declaration to subtract already-merged next-minor bytes from a patch candidate.

This rule is the lifecycle-level counterpart of the patch-release guard in the release-preparation workflow.

## When the next capability wave may start

The next milestone can be **planned** early.

Its implementation should start when the current line has enough stability that one of these is true:

1. adoption is stable and the expected maintenance delta can be completed before next-minor feature bytes merge; or
2. an explicit maintenance-line strategy allows current-line patches without contaminating them with next-minor work.

Do not block product planning merely because final Sonar cleanup is still running. Do prevent branch/release topology from making required maintenance impossible.

## Milestone closure rule

Close a release milestone when all of the following are true:

- the minor release is published and verified;
- adoption blockers discovered for that release line are fixed or explicitly assigned;
- release-specific architecture cleanup is complete or explicitly assigned outside the milestone with rationale;
- the broader self-architecture pass has no anonymous untriaged debt;
- the final whole-repository quality sweep is complete or remaining debt has explicit focused owners;
- any maintenance patch needed to distribute the stabilization result is published/verified;
- optional work that no longer belongs to the wave is deliberately moved rather than left ambiguously open.

Milestone closure is therefore an **engineering-wave closure**, not the same event as creating the `X.Y.0` tag.

## Agent decision rules

When an agent is planning or executing release-related work, it must first identify the current lifecycle phase.

### If the minor is not published

Ask from repository facts:

- which product lanes are still required;
- whether docs reflect shipped semantics;
- whether packed consumer-shaped acceptance exists/passes;
- whether an apparent cleanup task is actually a correctness blocker;
- whether known architecture debt is bounded and ratcheted.

Do not move broad cleanup into the critical path without release-risk evidence.

### If the minor is published but adoption is not stable

Prioritize real consumer blockers and patch distribution over next-minor feature implementation.

Do not classify an adoption blocker as optional refactoring.

### If adoption is stable and architecture cleanup is active

Preserve behavior and public contracts. Split work by responsibility boundary. Use the release delta and self-policy evidence to justify tasks.

### If architecture cleanup is complete and quality sweep is active

Treat Sonar as quality evidence on the final structure, not as architecture authority.

### If a next patch is requested

Use the concrete maintenance delta and current branch/ref facts. Never assume that milestone membership defines patch scope, and never hide unrelated next-minor bytes behind an artificially narrow declaration.

## Required issue/backlog language

Release-level stories should state their role explicitly, for example:

- `release-blocking product capability`;
- `consumer-shaped acceptance`;
- `optional/non-blocking release hygiene`;
- `post-release architecture cleanup`;
- `real-adoption correctness stabilization`;
- `final Sonar/quality cleanup`;
- `maintenance publication`.

Post-release cleanup stories should record the exact analyzed release range and distinguish release-created/amplified debt from unrelated legacy debt.

Quality cleanup should state whether it runs before or after architecture stabilization; the default is after.

## Non-goals

This process does not require:

- exactly one patch after every minor;
- zero technical debt at every minor cut;
- delaying a correct product release until every historical refactor is complete;
- treating Sonar as architecture authority;
- treating every milestone item as a release blocker;
- starting next-minor code on `main` while still pretending the same ref can produce a clean current-line patch;
- weakening policy, baselines, release scope, or acceptance gates to make a release appear complete.

The objective is a repeatable balance:

> **ship coherent capability quickly, prove real adoption, then restore the engineering baseline deliberately before the next capability wave accumulates on top of unresolved debt.**
