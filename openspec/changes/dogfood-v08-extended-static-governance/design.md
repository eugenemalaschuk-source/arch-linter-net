## Context

Checkpoint B already has a proven pattern for "one packed CLI, run scenarios against synthetic consumers, gate on immutable evidence": `CandidatePackageFeed`/`CheckpointBReleaseGateTests*.cs` (11 partial files, one per scenario family) produce `CheckpointScenarioResult` records via `WriteShardEvidence(shardId, scenarios, policyShape?)`; `tools/release/aggregate_checkpoint_b_evidence.py` and `tools/release/merge_checkpoint_b_platform_evidence.py` fail closed on missing/duplicate/unexpected/failed scenario IDs against fixed `_REQUIRED_SCENARIOS`/`_REQUIRED_SHARDS` sets; `.github/workflows/ci.yml` and `.github/workflows/release-nuget.yml` wire one shard per Make target into flat matrix entries. The release-scope declaration mechanism (`tools/release/create_release_scope_evidence.py`, schema `checkpoint-b-release-scope-declaration/v2`) is separately proven and stable since `0.6.4`.

Three synthetic consumer shapes already exist under `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/`, each independently proven for a narrower purpose:
- `modular-consumer/` — a 20-module + composition-host solution, currently exercised only for policy-check/consumer-cleanup scenarios, not topology/contract-surface/budgets/SARIF/change/health/report/badge.
- `api-surface-selector/` — one library project with orthogonal API-membership/role markers, already the fixture behind the `public-api-surface-selector-*` scenario family (#94/#525).
- `topology-review-unity/` — a Roslyn-compiled (no real Unity) Runtime→Gameplay→Editor asmdef layout, already proven for `topology capture/diff/verify` only.

No existing test chains policy check → analysis → topology → contract-surface → budgets → external SARIF → change/health/report/badge against one coherent fixture state. That chaining is genuinely new work; the fixtures and the "one packed CLI, scenario shard" orchestration around them are not.

## Goals / Non-Goals

**Goals:**

- Prove the documented v0.8 command chain (`docs/guides/single-tool-workflow.md` §§2–12) end to end, against the immutable Checkpoint B candidate only, using `modular-consumer` as the primary server/modular full-cycle fixture.
- Prove the canonical 5-state Health matrix (HEALTHY/DEBT/DEGRADING/FAILING/UNASSESSABLE) as bounded mutations of that same primary fixture's evidence, reusing the existing `ArchitectureHealthState`/`ArchitectureHealthGate` resolution already implemented in `ArchitectureHealthProjector`.
- Prove shape-specific boundaries for the library (`api-surface-selector`) and Unity-style (`topology-review-unity`) fixtures without re-running the full cycle for either.
- Add exactly one reviewed `0.8.0` release-scope declaration and extend the one existing test that enumerates shipped declarations.
- Register the new scenario family/shard consistently across every existing registry (Python sets, Make target, two workflow files) so a missing registration fails closed the same way an existing one would.

**Non-Goals:**

- No new Health algorithm, gate/health resolution logic, report renderer, or badge projector — `ArchitectureHealthProjector`, `ArchitecturePrReportProjector`, and `ArchitectureHealthBadgeProjector` are consumed as-is.
- No new release-candidate/package mechanism — `CandidatePackageFeed` and the existing manifest schema are reused unchanged.
- No second evidence/scenario-registry schema parallel to `_REQUIRED_SCENARIOS`/`_REQUIRED_SHARDS`.
- No Unity-specific Health/debt model; the Unity fixture proves topology/exposure boundaries only, through the same canonical Health path everything else uses.
- No private/real First Ice repositories as evidence.
- No implementation of the post-merge release rehearsal itself (it is a manual `workflow_dispatch`, tracked as a follow-up task, not code this change delivers).

## Decisions

### Primary full-cycle fixture: extend `modular-consumer`, not a new fixture

`modular-consumer` is the only existing shape with a real multi-module dependency graph (`Synthetic.Composition` host + `Synthetic.Modules.M01`…`M20`) capable of carrying a meaningful `strict_contract_surface_exposure` rule, a metric budget, and a non-trivial topology declaration. `aspnet-host` is a single-project shared-framework probe, not a governance-shaped consumer. Building a fourteenth fixture from scratch would violate the issue's explicit "reuse existing... do not invent new ones" instruction. Alternative considered: compose a brand-new "reference policy" fixture matching the issue's own example policy block (§6 of the docs guide) — rejected as unnecessary duplication when `modular-consumer` already has the module/layer shape that block illustrates.

### Base/current git state: wire `GitTestRepository` into an `AdoptionAcceptanceFixture` copy

`GitTestRepository` (proven in `tests/ArchLinterNet.Core.Tests/History/`) gives deterministic, git-native base/current commits with a `Commit(message) -> sha` API; `AdoptionAcceptanceFixture.Create(id)` gives a disposable working copy of a checked-in fixture. Neither currently wires into the other. The new shard's fixture setup will: materialize the `modular-consumer` copy as a temp directory, `git init` it (or copy it *into* a `GitTestRepository`-created root) as the base state, mutate it deterministically (e.g. touch one module to introduce a reviewed baseline entry, or leave it clean for HEALTHY), commit, mutate again for the current state, commit — giving two real commits of the same consumer repo. Alternative considered: fabricate two independent fixture copies with no git relationship — rejected because the issue requires "genuinely different deterministic base/current states... using a temporary Git repository/worktree pair," and change/report/health semantics depend on a real revision pair, not two unrelated snapshots.

### Health matrix states are produced by mutating the primary fixture's policy/baseline/evidence inputs, not by branching test logic

Each of the 5 states is a different *input* to the same unmodified `health` command path:
- HEALTHY: clean policy, no baseline debt, no waiver, all required evidence assessable.
- DEBT: a frozen baseline entry (matches `ArchitectureHealthProjectorTests.Project_FrozenBaselineEntry_IsReviewedDebtAndNotNewDebt`'s shape) — reviewed, not new.
- DEGRADING (deliberately blocking): a newly-added/broadened waiver under the default strict profile, or an enforced metric-budget regression — chosen because both make `gate=fail` while still being a `degrading` (not `failing`) dimension, per the issue's explicit "one intentionally blocking degrading case."
- FAILING: a live strict violation introduced in the current state, or an expired/invalid waiver.
- UNASSESSABLE: a required external-evidence binding pointed at the wrong revision/scope (proven pattern: `SarifEvidenceReaderTrustBindingTests.Read_WrongRequiredBinding_IsRejectedWithDimensionSpecificStatus`), and/or a newly-unmapped/ambiguous required topology subject.

This keeps the Health *engine* untouched (non-goal) and reuses the exact "wrong-revision/wrong-scope" and "frozen baseline" evidence shapes already proven at the unit level, now exercised through the packed CLI instead of in-process.

### Recursive exposure negative proof: extend `modular-consumer`'s contract-surface contract, mirror `ContractSurfaceExposureIndexTests`'s path-kind assertions

Add one `strict_contract_surface_exposure` contract to `modular-consumer`'s policy that a nested generic/wrapper position in one module deliberately violates, and assert on the resulting `ContractSurfaceExposurePayload.ExposurePath`/`CanonicalExposurePath` the same way `ContractSurfaceExposureIndexTests`/`ContractSurfaceExposureEvaluationTests` already do at the unit level — now through the installed CLI's JSON/SARIF output instead of in-process execution. Reuses the existing payload shape; adds no new evidence type.

### External SARIF fixture: synthesize inline, following `SarifEvidenceTestRepository`/`BuildSarif`

No checked-in `.sarif` sample exists anywhere in the repo; every existing SARIF test synthesizes one inline. The new shard follows the same pattern rather than introducing the first checked-in binary/JSON SARIF fixture, keeping the fixture deterministic and easy to mutate for the "valid current-context zero-result" vs "wrong-revision/wrong-scope" cases in one place.

### Registry additions follow the existing three-set-union pattern exactly

`_REQUIRED_SCENARIOS` in `aggregate_checkpoint_b_evidence.py` is a plain `set[str]` union of three named families; this change adds a fourth (`_V08_FULL_CYCLE_SCENARIOS`, name TBD during apply) rather than restructuring the union or introducing per-family metadata. `_REQUIRED_SHARDS` in `merge_checkpoint_b_platform_evidence.py` gains one new shard id. No schema version bump — both registries are internal Python sets, not versioned artifacts.

### Library/Unity shape-specific proofs stay in their own shard(s), not folded into the full-cycle shard

The issue explicitly says not to duplicate the full server cycle for coverage count. `api-surface-selector` and `topology-review-unity` get their own scenario IDs/shard(s) (or are added to already-adjacent existing shards if that avoids an unnecessary new shard — a specific apply-time decision, see Open Questions) proving only their shape-specific boundary (API surface reduction discipline for the library; Runtime/Editor exposure + required-subject mapping for Unity), each still ending at the same canonical Health/report path used by the primary fixture, per the issue's "no Unity-specific debt/Health model" instruction.

## Risks / Trade-offs

- [Wiring `GitTestRepository` into `AdoptionAcceptanceFixture` for the first time is new integration surface, not a reused path] → keep the wiring to the minimum needed (two commits, one working copy) and add focused tests for that wiring alone before building the full-cycle scenario on top of it.
- [A single "full-cycle" shard that chains ~12 CLI invocations is slow and any single-command flake fails the whole shard] → keep the shard's own scenario IDs granular enough (one per pipeline stage, not one monolithic scenario) so a failure identifies which stage broke, matching the existing pattern of many small scenario IDs per family rather than one opaque pass/fail.
- [Registering a new shard touches 6 files across 2 languages and 2 workflow files; a missed registration silently drops coverage rather than failing loudly] → the existing `_validate_scenario_union`/`_validate_shard_inventory` fail-closed checks already catch a missing scenario/shard at aggregation time; this change relies on those existing guards rather than adding new ones, and treats "all four gates raise clearly" as an implementation-time smoke test.
- [The Unity fixture's Runtime→Gameplay→Editor layout doesn't literally match "Runtime/Editor/Tests" from the issue text] → the issue's own body is not prescriptive about asmdef names; treat the existing shape as satisfying "Unity-style... Runtime/Editor declared topology" and only add a Tests-named asmdef if the topology/exposure proof genuinely needs a third boundary, not to match issue prose literally.
- [`modular-consumer`'s 20 modules may be more than a budget/exposure scenario needs, increasing packed-CLI run time] → scope the new contract-surface/budget additions to 1–2 modules, not all 20; the fixture's size is a asset for topology/module-coverage proofs, not a requirement to touch every module.
- [Post-merge rehearsal cannot be validated before merge] → tasks.md tracks it as an explicit, separate, non-implementation follow-up task with its own closure criteria (run URL, source SHA, aggregate result recorded on #524), so the PR's own acceptance criteria never silently absorb it.

## Migration Plan

Not applicable in the deploy/rollback sense — this is additive test/tooling/CI surface with no production runtime behavior change. Rollback is reverting the added files/registry entries; no data migration, no public API change, no package version behavior change (the `0.8.0.json` declaration only takes effect when a candidate manifest actually reports version `0.8.0`, which no other workflow does today).

## Open Questions

- Exact v0.8 scenario ID list and shard boundaries (one shard vs. the full cycle split across 2–3 shards for parallelism) — resolve during `opsx-apply` by first drafting the scenario list against the docs guide's 12 stages, then deciding shard granularity from actual measured run time.
- Whether library (`api-surface-selector`) and Unity (`topology-review-unity`) shape-specific proofs need their own new shard(s) or can be added as new scenarios inside their existing adjacent shards (`public-api-surface-selector-*`, or a topology-focused shard) — resolve during apply once the new scenario IDs are drafted, favoring reuse of an existing shard over adding a new one where the existing shard's Make target/CI matrix entry already fits.
- Precise mechanism for the DEGRADING-but-`gate=pass`-elsewhere note in the issue ("other degrading evidence may coexist with gate=pass when its owning authority is advisory") — whether the primary fixture needs a second, advisory-authority degrading case in addition to the one deliberately-blocking case, or whether documenting the general rule without a second fixture instance is sufficient. Default to documenting the rule in the scenario's assertions/comments without adding a second mutation, since the issue marks only the blocking case as required.
- Exact `story` issue number and required/excluded/delivered item list for `tools/release/scopes/0.8.0.json` — the issue text gives an explicit list (required: #504,#91,#92,#93,#95,#633,#672,#684,#706; excluded: #510,#673; delivered: #742; story likely #90) to transcribe verbatim during apply, re-verifying each issue's current state via `gh issue view` before finalizing.
