## Why

Milestone 6 / v0.8.0 ("Extended static governance") has delivered every owning capability (#504, #91, #92, #93, #95, #633, #684) and its last reliability blocker (#762/#706/PR #763) is now merged, but nothing yet proves the *composed* v0.8 workflow — policy check through Architecture Health/report/badge — actually works end to end against one immutable packed candidate, the way the public `docs/guides/single-tool-workflow.md` guide describes it. There is also no reviewed `0.8.0` release-scope declaration, so a `release-nuget.yml` rehearsal with `version_override=0.8.0` cannot pass. Issue #524 is the release-acceptance task that proves both, without introducing any new product semantics.

## What Changes

- Add `tools/release/scopes/0.8.0.json`, a reviewed `checkpoint-b-release-scope-declaration/v2` declaration (`story=90`) naming the v0.8 required/excluded/delivered issue inventory, and extend the one existing regression test that enumerates shipped declarations (`tools/release/tests/test_create_release_scope_evidence.py::test_shipped_declarations_preserve_both_reviewed_release_authorities`) to cover it.
- Add one new Checkpoint B packed shard (`test-packed-artifact-v08-full-cycle`) that drives the full documented v0.8 command chain — `policy check` → analysis/applicability → `topology capture/diff/verify` → contract-surface governance → `policy weakening`/`gate` → `measure` + an enforced budget → required current-context external SARIF binding → `change snapshot/report` → `health` → `report pr` → `badge architecture-health` — against one coherent primary fixture, through the installed packed CLI only (never `dotnet run`/`ProjectReference`).
- Extend the existing `_REQUIRED_SCENARIOS` release-evidence registry (`tools/release/aggregate_checkpoint_b_evidence.py`) and `_REQUIRED_SHARDS` (`tools/release/merge_checkpoint_b_platform_evidence.py`) with the new v0.8 scenario family so missing/duplicate/unexpected/failed v0.8 scenarios fail platform/release evidence exactly like every existing Checkpoint B family, and wire the new shard into both `.github/workflows/ci.yml` (Windows + macOS packed-artifact matrices) and `.github/workflows/release-nuget.yml` (4-platform `checkpoint-b-shards` matrix).
- Prove, within that same full-cycle shard or focused sibling shards reusing the same primary fixture, the canonical Health matrix (HEALTHY / DEBT / deliberately-blocking DEGRADING / FAILING / UNASSESSABLE), a recursive first-party contract-surface exposure negative proof, and cross-projection agreement (JSON/SARIF/Testing finding identity; Health/report/badge overlapping facts) — all against evidence already produced by the primary run, with no second Health/report/badge implementation.
- Reuse the existing `AdoptionAcceptance` synthetic fixtures for the three required consumer shapes instead of inventing new ones: `Fixtures/modular-consumer` (server/modular full-cycle authority), `Fixtures/api-surface-selector` (library/package #94/#525 shape-specific proof), `Fixtures/topology-review-unity` (Unity-style shape-specific proof) — extending each only as far as its shape-specific proof requires, not duplicating the full cycle three times.
- Record the mandatory post-merge, non-publishing `release-nuget.yml` rehearsal (`publish=false`, `version_override=0.8.0`) as a tracked follow-up: it cannot run until this change's PR is merged, and #524 cannot close until that rehearsal passes on all four Checkpoint B platforms and its evidence is recorded on the issue.

## Capabilities

### New Capabilities

None. This change composes and proves existing capabilities; it does not introduce a new contract family, Health algorithm, report/badge implementation, or release-candidate mechanism (explicit non-goal of #524).

### Modified Capabilities

- `checkpoint-b-release-evidence`: adds the v0.8 full-cycle required scenario family (and its shard) to the scenario/shard inventories that already gate platform and aggregate release evidence, and adds the reviewed `0.8.0` entry to the release-scope declaration authority the same capability's existing "Publication authorization proves the release scope is closed" requirement already governs.

## Impact

- **Python release tooling**: `tools/release/scopes/0.8.0.json` (new); `tools/release/aggregate_checkpoint_b_evidence.py`, `tools/release/merge_checkpoint_b_platform_evidence.py` (new scenario/shard set additions); `tools/release/tests/test_create_release_scope_evidence.py` and any `tools/release/tests/` fixtures that hardcode the current scenario/shard counts.
- **C# test surface**: new `CheckpointBReleaseGateTests.*.cs` partial file(s) under `tests/ArchLinterNet.Core.Tests/`, reusing `CandidatePackageFeed`/`WriteShardEvidence`; possible small, shape-scoped extensions to the reused `AdoptionAcceptance` fixtures (e.g. binding an SARIF requirement, adding a budget, adding one recursive-exposure contract) — no changes to production `src/` behavior.
- **Build/CI**: `make/packed-artifact.mk` (new target), `.github/workflows/ci.yml` (two matrix entries), `.github/workflows/release-nuget.yml` (one matrix entry, auto-fans to 4 platforms).
- **Docs**: only if #524 surfaces drift between `docs/guides/single-tool-workflow.md` and actual product behavior — fixed at the owning capability/doc, not hidden in test glue, per the issue's explicit instruction.
- **No production `src/` API, schema, or Health/report/badge algorithm changes.**
- **Out of this PR's control**: the post-merge 4-platform release rehearsal, which is a separate manual `workflow_dispatch` run recorded on #524 after merge.

## Amendment (2026-09-04, post-archive)

Line 30's "no production `src/` API, schema, or Health/report/badge algorithm changes" no longer holds. Multi-review of PR #766 (which implements this change) surfaced real product defects that could only be fixed at the owning capability, not in test glue -- the same escape hatch this proposal explicitly ruled out for docs drift (line 29) applies equally here, so these are recorded rather than hidden:

- `measure` and `baseline generate`/`update`/`prune` could not resolve `analysis.target_assemblies` for any genuinely external target repository -- a confirmed product gap, not a test gap. Fixed by adding `--ensure-built`/`--no-restore`/`--configuration`/`--framework`/`--platform`/`--runtime` to all four CLI commands, matching the surface `validate`/`health`/`gate`/`topology`/`baseline verify`/`diff` already had. This is a genuine, reviewed public API addition (new members on `ArchitectureMetricMeasurementRequest`, `BaselineGenerationRequest`/`Outcome`, `BaselineUpdateRequest`/`Outcome`, `BaselinePruneRequest`/`Outcome`, and a new shared `BaselineBuildStateRequest` base type extracted to remove the resulting SonarCloud-flagged duplication), captured in `architecture/api/ArchLinterNet.Core.public-api.txt`.
- `PostBuildArtifactEvidenceRefresher` never cleared "missing project build output" discovery diagnostics for projects `--ensure-built` had just built successfully, causing intermittent `--ensure-built` failures.
- `ArchitectureContractExecutionContext.IsIgnored`/`IsIgnoredWithAliases` recorded every suppressed occurrence into baseline-comparison candidates regardless of whether the suppression came from the loaded baseline or a policy-authored structured waiver, letting an active waiver surface as new persistent debt. Fixed via a new `IsFromLoadedBaseline` provenance flag (internal, not public API) and a corresponding `ArchitectureBaselineComparer` behavior change.
- `ArchitectureBaselineComparer.Compare` only used structured finding identity for `version: 2` baselines, silently falling back to legacy display-pair matching for `version: 3` even though the loader treats both as fully structured -- a real comparer behavior change, not test-only.

None of these introduce a new contract family, Health algorithm, report/badge implementation, or release-candidate mechanism -- the non-goal in the "New Capabilities" section above still holds. But "no production `src/` behavior changes" was inaccurate the moment review uncovered defects that could only be fixed at the source; this amendment is the acknowledgment the original text should have anticipated.
