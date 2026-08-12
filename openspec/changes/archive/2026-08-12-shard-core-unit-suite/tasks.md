## 1. Fixture-duration inventory and documentation

- [x] 1.1 Capture a fresh local `make test-unit` TRX baseline; rank `ArchLinterNet.Core.Tests`
      fixture classes by measured NUnit `<duration>`.
- [x] 1.2 Write `docs/internal/core-unit-shard-inventory.md`: methodology, ranked table, the
      chosen shard-1 token list with rationale (measured cost and/or category:
      Roslyn/IL, filesystem/process, project-resolution, reflection-heavy), and how to rebalance
      shards later. Index it from `docs/internal/README.md`.

## 2. Shard filters and Make targets

- [x] 2.1 Add `TEST_CORE_UNIT_SHARD_1_FIXTURES` / `TEST_CORE_UNIT_SHARD_1_FILTER` to
      `make/test.mk`, in the same pure-OR `FullyQualifiedName~X|...` shape as
      `TEST_E2E_FIXTURES` — no parenthesized filters.
- [x] 2.2 Add `TEST_CORE_UNIT_SHARD_2_FILTER` as `TEST_UNIT_FILTER` plus one
      `&FullyQualifiedName!~<token>` negation per shard-1 token.
- [x] 2.3 Add `test-unit-core-1` and `test-unit-core-2` targets, each running
      `dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore
      --no-build --filter <shard filter>` (scoped to the Core.Tests project, not the `.slnx`).
- [x] 2.4 Add `test-unit-other` target running `ArchLinterNet.CEL.Tests` and
      `ArchLinterNet.Cli.Tests` (unfiltered).
- [x] 2.5 Redefine `test-unit` as the parallel union of `test-unit-core-1`, `test-unit-core-2`,
      `test-unit-other`, mirroring the existing `test:` target's parallel/wait-all-then-check
      pattern.
- [x] 2.6 Verify by direct experiment (as during exploration) that the chosen shard-1 tokens have
      zero substring collisions with each other, with `TEST_E2E_FIXTURES`, and with
      `TEST_PACKED_ARTIFACT_FILTER`, using a `dotnet vstest --ListFullyQualifiedTests` dump.
- [x] 2.7 Update the root `Makefile` help text with the new targets.

## 3. Mechanical shard-membership validation

- [x] 3.1 Add `tools/scripts/verify_core_unit_shards.py`: builds
      `ArchLinterNet.Core.Tests.csproj` if needed, runs
      `dotnet vstest <dll> --ListFullyQualifiedTests`, parses the shard/E2E/packed-artifact
      filter tokens out of `make/test.mk`, classifies every discovered FQN, and fails on a dead
      shard token or an E2E/packed-artifact leak. Prints the resulting partition counts on
      success.
- [x] 3.2 Add a lean pytest test for the script's classification/dead-token/leak-detection logic
      under `tools/scripts/tests/`, using a small fixture FQN list rather than a real build.
- [x] 3.3 Add `lint-test-shard-membership` to `make/lint.mk`, wired into the `lint` aggregate
      target.

## 4. CI workflow

- [x] 4.1 Restructure `unit_tests` in `.github/workflows/ci.yml`: explicit 4-entry
      `matrix.include` (Windows/shard 1, Windows/shard 2, Apple Silicon macOS/shard 1, Apple
      Silicon macOS/shard 2), matching the existing `os`/`name` enumeration style used by
      `e2e_tests`/`packed_artifact_tests`.
- [x] 4.2 Each leg runs `make test-unit-core-${{ matrix.shard }}`; shard-1 legs additionally run
      `make test-unit-other`.
- [x] 4.3 Size `timeout-minutes` per leg conservatively below the current unsharded 30-minute
      budget, without repeating the flaky-cancellation mistake #481 already documented (prefer
      headroom over a tight cap with only local, not CI, timing evidence). Set to 20m.
- [x] 4.4 Confirm no `needs:` edges are introduced between shard legs or against
      `e2e_tests`/`packed_artifact_tests`/the PR-validation jobs.

## 5. Validation

- [x] 5.1 Run `make test-unit-core-1`, `make test-unit-core-2`, `make test-unit-other`,
      `make test-unit` locally; confirm combined pass/fail and test counts match the pre-change
      `make test-unit` baseline exactly (no test lost, none duplicated). Confirmed: shard 1 (131)
      + shard 2 (2436 executed) + CEL/Cli (584+489) = 3640, exactly matching the pre-change
      baseline's combined `Total` counts; the 2571-vs-2567 discovery-vs-execution gap is a
      pre-existing suite characteristic, not introduced by sharding.
- [x] 5.2 Run the new shard-membership validator directly and confirm it passes against the
      actual repository state.
- [x] 5.3 Run `make fmt` and inspect formatting changes. `fmt-docs` renumbered the inventory
      doc's methodology list to the tool's consistent-`1.`-style convention (harmless, no content
      change). `fmt-csharp` also reformatted `BaselineWriteGate.cs`, a file this change never
      touches — reverted it to keep the diff scoped. Confirmed via `diff` that the working-tree
      copy is byte-identical to `origin/main`, and via PR #487's own CI run that GitHub's Linux
      `repository_lint` job (`dotnet format --verify-no-changes`) passes clean against that same
      file: this is a local Windows/SDK-patch `dotnet format` false positive on this machine, not
      a real formatting defect on `main`.
- [x] 5.4 Run `make lint-workflows` (actionlint + zizmor + prettier --check) against the edited
      `ci.yml`. `zizmor` could not be installed locally (needs a Rust toolchain + MSVC build
      tools not present on this machine); verified `actionlint` (exit 0) and
      `prettier --check` (passed) individually — matches the precedent already recorded in the
      archived `2026-08-12-parallelize-pr-validation-jobs` change's tasks.md for the same gap.
      The `workflow_quality` CI job runs all three, including `zizmor`, on the PR itself.
- [x] 5.5 Run `make acceptance`; fix any issue-related failures and rerun until green. No
      issue-related failures found. Two pre-existing, environment-only failures were present and
      independently confirmed unrelated to this change:
      - `lint-dotnet-format` on `BaselineWriteGate.cs` (see 5.3 above) — local Windows/SDK-patch
        `dotnet format` false positive; the file is byte-identical to `origin/main` and PR #487's
        `Repository Lint` CI job passes clean against it on GitHub's Linux runner.
      - `CheckpointBReleaseGateTests.PackedCandidate_InstallsFromAnIsolatedFeedAndPassesTheSyntheticAdopterMatrix`
        (packed-artifact bucket) fails on a `layer-overlap` assertion. Reproduced identically with
        this change's working-tree changes stashed (i.e. against pristine `origin/main` HEAD), and
        confirmed absent on GitHub-hosted CI: PR #487's `Packed Artifact Test Suite` passed on both
        Windows (6m36s) and Apple Silicon macOS (2m32s). Local-machine-only (isolated-feed/NuGet
        environment quirk), not a real regression, and unrelated to any file this change touches
        (no C#/production code changed).
      Every other lint and test signal passed: `lint-code-size` (1 pre-existing warning, unrelated
      file), `lint-architecture`, `lint-docs`, the new `lint-test-shard-membership`, the complete
      unit bucket, and the complete E2E bucket.

## 6. Spec synchronization and archive

- [x] 6.1 Compare the implemented Make targets, validator, and CI matrix against `design.md` and
      the delta spec; reconcile any divergence. No divergence found — implementation matches the
      documented design decisions (54-token count-rebalanced shard 1, project-scoped filters,
      unsharded coverage, lint-wired membership check, 4-leg CI matrix).
- [x] 6.2 Run `openspec validate --all`. `spec/github-actions-ci` and `change/shard-core-unit-suite`
      both pass. 5 unrelated pre-existing specs fail (adoption-migration-guidance,
      adoption-stabilization-compatibility, checkpoint-b-release-evidence, diagnostics-model,
      layer-contracts) — confirmed untouched by this change (`git status openspec/specs/` shows no
      modifications).
- [x] 6.3 Run `openspec archive shard-core-unit-suite` and inspect the resulting
      `openspec/specs/github-actions-ci/spec.md`. The three new requirements landed cleanly
      appended to the existing spec. Re-ran `openspec validate --all --strict`: same result as
      before archiving (`github-actions-ci` passes; the same 5 unrelated pre-existing specs
      fail).

## 7. Pull request

- [x] 7.1 Push the feature branch and open a PR referencing #478 (`Closes #478`), noting the
      already-satisfied dependencies on #475 and #477. Opened as
      https://github.com/eugenemalaschuk-source/arch-linter-net/pull/488.
- [ ] 7.2 After the PR's own CI run completes, record real per-leg shard timings (per platform)
      as timing evidence, including the pre-sharding baseline comparison the issue's acceptance
      criteria ask for.
