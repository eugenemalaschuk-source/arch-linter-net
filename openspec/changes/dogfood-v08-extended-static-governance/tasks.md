## 1. Release-scope declaration (small, self-contained, low-risk — do first)

- [x] 1.1 Re-verify the current state of every issue #524 names for the v0.8 declaration (`gh issue view <n>`) — required {504,91,92,93,95,633,672,684,706}, excluded {510,673}, delivered {742}, story owner (#90) — before transcribing numbers into the declaration.
- [x] 1.2 Add `tools/release/scopes/0.8.0.json` (schema `checkpoint-b-release-scope-declaration/v2`, `declaration_id: v0.8.0-extended-static-governance`, `release_target: 0.8.0`, `story: 90`), with `required_items`/`excluded_items`/`delivered_items` per the verified list; do NOT list #524 itself as a required item.
- [x] 1.3 Extend `tools/release/tests/test_create_release_scope_evidence.py::test_shipped_declarations_preserve_both_reviewed_release_authorities` to assert the 7th declaration (`len(declarations) == len(by_target) == 7`, `"0.8.0"` in the target set, and its story/required/excluded/delivered assertions).
- [x] 1.4 Run `python3 -m pytest tools/release/tests/test_create_release_scope_evidence.py -q` and fix until green. (Ran via `make test-release-evidence`: 272 passed.)

## 2. Base/current fixture wiring (new integration surface — build and test in isolation before the full-cycle scenario depends on it)

- [x] 2.1 Add a small helper that materializes an `AdoptionAcceptanceFixture` copy of `modular-consumer` as a `GitTestRepository` root (or copies it into one), producing two deterministic commits (base, current) via `Commit(message) -> sha`. (`GitVersionedAdoptionFixture`, plus `GitTestRepository.CreateAt`.)
- [x] 2.2 Add a focused test proving the wiring alone: two distinct commits exist, each is checkoutable/readable independently, and the working copy used for "current" is not accidentally shared state with "base". Found and fixed a real pre-existing deadlock in `GitTestRepository`: `git add -A` across modular-consumer's 60+ files produces enough CRLF-conversion warnings on stderr to fill the pipe buffer, and the old sequential `ReadToEnd()` on stdout-then-stderr deadlocked waiting for a process that was itself blocked writing to a full stderr pipe. Fixed by reading both streams concurrently (`ReadBothStreamsToEnd`); verified all 207 existing `History` tests still pass.
- [ ] 2.3 Decide and record (in this file or a short code comment) the specific deterministic mutation `modular-consumer` undergoes between base and current (e.g., one module touched to introduce a reviewed baseline entry) — keep it minimal and legible.

## 3. Primary server/modular full-cycle scenario

- [ ] 3.1 Draft the exact v0.8 scenario ID list against the docs guide's stages (policy-check, applicability, topology capture/diff/verify, contract-surface, weakening/gate, measure+budget, external-evidence, change, health, report-pr, badge) — one scenario ID per stage, not one monolithic pass/fail.
- [ ] 3.2 Extend `modular-consumer`'s policy with: a `strict_contract_surface_exposure` contract exercising a nested generic/wrapper exposure violation in one module; a metric budget on a bounded scope; an `external_evidence` requirement.
- [ ] 3.3 Add new `CheckpointBReleaseGateTests.<Family>.cs` partial file(s) implementing the drafted scenarios, driving the installed packed CLI only, through the base/current fixture from Section 2, following the existing `CandidatePackageFeed`/`WriteShardEvidence` pattern.
- [ ] 3.4 Prove the 5-state canonical Health matrix as bounded mutations of the same primary fixture's evidence (HEALTHY, DEBT via a frozen baseline entry, deliberately gate-blocking DEGRADING via a new/broadened waiver or budget regression, FAILING via a live strict violation or expired/invalid waiver, UNASSESSABLE via wrong-revision/wrong-scope required evidence and/or an unmapped required topology subject).
- [ ] 3.5 Prove the recursive contract-surface exposure negative proof from 3.2 asserts on real `ContractSurfaceExposurePayload.ExposurePath`/`CanonicalExposurePath` evidence (not a coarse dependency-direction check).
- [ ] 3.6 Synthesize the required external SARIF evidence inline (following `SarifEvidenceTestRepository`/`BuildSarif`), covering: valid current-context zero-result; required-but-missing; wrong-revision; wrong-scope.
- [ ] 3.7 Assert projection parity where the docs guide's outputs overlap: JSON/SARIF/Testing finding identity; Health/report/badge category, gate, effective-rule-count, and ignore/waiver-debt totals.
- [ ] 3.8 Register the new scenario family in `tools/release/aggregate_checkpoint_b_evidence.py` (`_REQUIRED_SCENARIOS`) and its shard(s) in `tools/release/merge_checkpoint_b_platform_evidence.py` (`_REQUIRED_SHARDS`).
- [ ] 3.9 Add the corresponding `make/packed-artifact.mk` target(s) (`TEST_PACKED_ARTIFACT_V08_FULL_CYCLE_FILTER` + `test-packed-artifact-v08-full-cycle`), including `.PHONY` entries.
- [ ] 3.10 Run the new target(s) locally against a locally-packed candidate and fix until green.

## 4. Library and Unity-style shape-specific proofs

- [ ] 4.1 Decide whether the library (`api-surface-selector`) shape-specific proof needs a new shard or fits inside an existing `public-api-surface-selector-*` shard as new scenario(s); implement accordingly, proving only the shape-specific boundary (materially smaller selected surface, role preservation, recursive first-party escape rejection) — do not re-run the full cycle.
- [ ] 4.2 Decide whether the Unity-style (`topology-review-unity`) shape-specific proof needs a new shard or fits inside an existing/new topology-focused shard; implement accordingly, proving declared Runtime/Editor topology, exhaustive required-subject mapping, unmapped/ambiguous-subject fail-closed, and runtime/public-surface exposure rejection, through the same canonical Health/report path (no Unity-specific debt/Health model).
- [ ] 4.3 If either decision in 4.1/4.2 adds a genuinely new shard, register it the same way as Section 3.8/3.9 (registries + Make target); if it extends an existing shard, update that shard's scenario list and any Python tests hardcoding its current scenario set.

## 5. CI wiring

- [ ] 5.1 Append the new shard(s)' matrix entries to `.github/workflows/ci.yml`'s `packed_artifact_windows_shards` and `packed_artifact_macos_shards`.
- [ ] 5.2 Append the same new shard(s) to `.github/workflows/release-nuget.yml`'s `checkpoint-b-shards` job `shard:` list (the existing 4-platform `platform:` cross-product covers all platforms automatically).
- [ ] 5.3 Update `tools/release/tests/test_aggregate_checkpoint_b_evidence.py` and `tools/release/tests/test_merge_checkpoint_b_platform_evidence.py` fixtures to match the new `_REQUIRED_SCENARIOS`/`_REQUIRED_SHARDS` sets.
- [ ] 5.4 Run `python3 -m pytest tools/release/tests/ -q` and fix until green.

## 6. Docs/spec drift (only if surfaced)

- [ ] 6.1 If any stage of Section 3 reveals drift between `docs/guides/single-tool-workflow.md` and actual CLI behavior, fix the owning capability or the doc directly (not test glue), and note the fix here.

## 7. Spec synchronization and archive

- [ ] 7.1 Compare the implemented scenarios/tests against `specs/checkpoint-b-release-evidence/spec.md` in this change; adjust the delta spec if implementation details diverged from the drafted scenarios.
- [ ] 7.2 Run `openspec validate --all` and fix until green.
- [ ] 7.3 Run `openspec archive dogfood-v08-extended-static-governance` and inspect the rebuilt `openspec/specs/checkpoint-b-release-evidence/spec.md`.

## 8. Validation and PR

- [ ] 8.1 `make fmt` on changed files; inspect the diff.
- [ ] 8.2 Focused validation: new/changed C# tests, new/changed Python tests, relevant lint (`make lint-architecture`, `make public-api-check` if touched).
- [ ] 8.3 Open exactly one PR targeting `main`, `Closes #524` when the PR itself satisfies the issue's PR-level acceptance criteria (new shard consumes the immutable candidate; candidate manifest/version/source binding passes; PR packed acceptance passes on Windows x64 and Apple Silicon macOS; required repository lint/unit/E2E/package-validation/CodeQL/architecture gates are green; Architecture PR Report publication remains valid; release-scope declaration/tooling tests pass; no source-tree product binary substitutes for the packed candidate) — leaving the post-merge rehearsal (Section 9) as an explicit follow-up in the PR body, not implied as done.

## 9. Mandatory post-merge rehearsal (NOT delivered by this change's implementation — separate follow-up after merge)

- [ ] 9.1 After this change's PR merges to `main`, trigger `release-nuget.yml` via `workflow_dispatch` with `publish=false` and `version_override=0.8.0` from the merged commit.
- [ ] 9.2 Confirm the run selects the reviewed `0.8.0` release-scope declaration, all required items are closed, and the real Checkpoint B matrix passes on Linux x64, Windows x64, macOS arm64, and macOS x64.
- [ ] 9.3 Confirm no NuGet.org publication, no GitHub Release/tag creation, and no Pages/docs deployment occurred.
- [ ] 9.4 Record on issue #524, before closing it: the rehearsal run URL/ID, the merged source SHA, the candidate manifest/version, and the aggregate Checkpoint B result.
- [ ] 9.5 Only after 9.1-9.4 succeed: close #524, then proceed with the issue's own "After #524 passes" follow-ups (update/close #90, move #510/#673 out of milestone 6 if unfinished) — these are separate from this change.
