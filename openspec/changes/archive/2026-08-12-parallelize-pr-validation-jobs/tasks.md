## 1. Workflow decomposition

- [x] 1.1 Remove the monolithic `pull_request_validate` job from `.github/workflows/ci.yml`.
- [x] 1.2 Add `workflow_quality` job: checkout only, actionlint, zizmor, Prettier check on
      `.github/workflows/*.yml` — no .NET setup/restore.
- [x] 1.3 Add `repository_lint` job: checkout, setup uv, setup .NET, cache NuGet, restore,
      `make lint`.
- [x] 1.4 Add `coverage_sonar` job: checkout with `fetch-depth: 0`, setup uv, setup .NET, setup
      Java, cache NuGet, restore, cache Sonar, install SonarScanner, validate Sonar config, Sonar
      begin (trusted PRs), `make test-coverage`, `make test-tooling-coverage`, resolve coverage
      report paths, Codecov upload (trusted PRs), Sonar end, PR summary / secret-skip explanation
      steps — no E2E or packed-artifact invocation.
- [x] 1.5 Add `architecture_coverage` job: checkout with `fetch-depth: 0`, setup uv, setup .NET,
      restore, build `ArchLinterNet.Cli` and `ArchLinterNet.Testing`, collect changed first-party
      files, run `make architecture-coverage-ci`, upload strict/audit/report artifacts, publish/
      update the PR comment, fail the job if strict coverage failed — `permissions:
      pull-requests: write`.
- [x] 1.6 Add `tooling_support_tests` job: checkout, setup uv, `make test-architecture-coverage-report`,
      `make test-release-evidence`.
- [x] 1.7 Confirm no `needs:` edges exist between the five new jobs or against
      `unit_tests`/`e2e_tests`/`packed_artifact_tests`.
- [x] 1.8 Confirm `main_badge_refresh` still uses `test-coverage-main-ci` (the coverage-only
      contract from #475) and needs no change.

## 2. Validation

- [x] 2.1 Run `make lint-workflows` (actionlint + zizmor + prettier --check) against the edited
      `ci.yml`. (`make lint-workflows` itself fails on a pre-existing, unrelated shellcheck finding
      in `package-validation.yml` on `main`; verified actionlint/zizmor/prettier individually
      against `ci.yml`, all clean.)
- [x] 2.2 Run `make fmt` and inspect formatting changes. No changes produced.
- [x] 2.3 Run `make acceptance`. Skipped by explicit repository-owner direction: this task's only
      change is CI job topology, not product code or Make targets, so local acceptance adds no
      signal beyond actionlint/zizmor/prettier already run in 2.1 — validated instead by letting
      the real PR's own CI run exercise the new jobs.
- [x] 2.4 Fix any issue-related failures and rerun until green. No issue-related failures found.

## 3. Spec synchronization

- [x] 3.1 Compare the implemented `ci.yml` against `design.md` and the delta spec; adjust either
      the implementation or the spec text if they diverge. No divergence found.
- [x] 3.2 Run `openspec validate --all`.
- [x] 3.3 Run `openspec archive parallelize-pr-validation-jobs` and inspect the resulting
      `openspec/specs/github-actions-ci/spec.md`.

## 4. Pull request

- [ ] 4.1 Push the feature branch and open a draft PR referencing #477, noting the (already
      satisfied) dependency on #475.
- [ ] 4.2 After the PR's own CI run completes, record job durations as timing evidence in the PR
      description or a follow-up comment.
