## Context

`.github/workflows/ci.yml`'s `pull_request_validate` job runs, in one Ubuntu runner and in this
order: workflow lint (actionlint/zizmor/Prettier), .NET restore, `make lint`, SonarCloud begin,
coverage-only unit tests, Python tooling coverage, Codecov upload, SonarCloud end, Python tooling
tests (report-generator, release-evidence), a changed-files diff, strict/audit architecture
coverage, artifact uploads, and the PR comment. #475 already extracted `unit_tests`, `e2e_tests`,
and `packed_artifact_tests` into independent per-platform matrix jobs and made the coverage target
unit-only. This change finishes the same decomposition for what remains in
`pull_request_validate`.

One real cross-step dependency exists today that the split must not silently break:
`lint-architecture` (part of `make lint`) builds `ArchLinterNet.Cli` and `ArchLinterNet.Testing`,
and the later `architecture-strict-json`/`architecture-audit-json` steps run
`dotnet run --no-build --project ArchLinterNet.Cli ...`, relying on that earlier build because
they execute later in the *same job* on the *same runner filesystem*. Once lint and architecture
coverage become separate jobs (separate runners, fresh checkouts), the architecture coverage job
must build the CLI project itself.

## Goals / Non-Goals

**Goals:**
- Break `pull_request_validate` into independently diagnosable jobs along real dependency
  boundaries, matching the issue's five target jobs: Workflow Quality, Repository Lint,
  Coverage + Sonar, Architecture Coverage, Tooling/Support Tests.
- Let unrelated checks schedule and fail concurrently; `workflow_quality` in particular must not
  wait on any .NET restore.
- Preserve every existing signal: lint checks, coverage collection/thresholds, Sonar quality gate
  enforcement, Codecov upload, architecture strict/audit artifacts and PR comment, and the Python
  tooling test suites (both the coverage and non-coverage invocations that exist today).
- Preserve fork/Dependabot behavior: all non-secret validation still runs; secret-backed Sonar/
  Codecov steps skip with the existing explanatory step.
- Keep `main_badge_refresh` on the coverage-only contract `test-coverage-main-ci` already
  established by #475 (no change needed there beyond confirming it).

**Non-Goals:**
- Sharding the Core unit test suite (tracked separately).
- Changing coverage thresholds or Sonar quality gate policy.
- Removing validation from fork PRs.
- Ruleset/required-status-check configuration (deferred to the governance task).
- Redesigning the release/publish workflows.

## Decisions

- **Five jobs, no shared job for lint + architecture coverage.** The issue explicitly separates
  Repository Lint from Architecture Coverage, and they have no real dependency on each other today
  (only the incidental same-job build reuse noted above). Splitting them means Architecture
  Coverage independently builds `ArchLinterNet.Cli`/`ArchLinterNet.Testing` before running
  `architecture-strict-json`/`architecture-audit-json`/`architecture-coverage-markdown` — the same
  build steps the on-demand `make architecture-coverage-report` target already performs locally, so
  this isn't a new pattern, just moving it into CI.
- **Coverage + Sonar stays one job.** SonarScanner for .NET requires `begin` → build/test → `end`
  in a single process lineage on one runner; splitting the Sonar lifecycle across jobs isn't
  supported. Python tooling coverage (`make test-tooling-coverage`) stays in this job too because
  its `coverage-python.xml` output feeds the same Sonar analysis via
  `sonar.python.coverage.reportPaths` — moving it out would force artifact-passing for no benefit.
- **Tooling/Support Tests re-runs the same two Python suites without coverage.** This preserves
  today's actual behavior: `pull_request_validate` already runs
  `test-architecture-coverage-report`/`test-release-evidence` as standalone steps *and* runs the
  same suites again with `--cov` via `test-tooling-coverage` for Sonar/Codecov. The split keeps
  both invocations, just in different jobs, rather than trying to collapse them (collapsing would
  be a behavior change requiring `coverage_sonar` to depend on `tooling_support_tests`, which
  reintroduces an artificial `needs:` edge for no signal gain).
- **No `needs:` edges between the five new jobs.** None of them consumes another's output; each
  gets its own checkout, and Architecture Coverage builds what it needs independently. This matches
  the `unit_tests`/`e2e_tests`/`packed_artifact_tests` precedent from #475.
- **`fetch-depth: 0` only where full history is used.** `coverage_sonar` keeps `fetch-depth: 0`
  (SonarCloud PR analysis wants full branch-comparison history, matching today's job). Architecture
  Coverage keeps `fetch-depth: 0` too, since its changed-files diff step does
  `git diff origin/$PR_BASE_REF...HEAD`. `workflow_quality`, `repository_lint`, and
  `tooling_support_tests` use the default shallow checkout, matching the existing
  `unit_tests`/`e2e_tests`/`packed_artifact_tests` jobs, since none of them run diffs against the
  base branch.
- **Java/SonarScanner setup stays confined to `coverage_sonar`.** No other new job needs Java or
  the SonarScanner CLI.
- **`uv` (Python) setup is added to `repository_lint`, `coverage_sonar`, `architecture_coverage`,
  and `tooling_support_tests`** — each runs at least one Python-backed Make target
  (`lint-code-size`/`lint-docs`, `test-tooling-coverage`, `architecture-coverage-markdown`,
  `test-architecture-coverage-report`/`test-release-evidence` respectively). `workflow_quality`
  does not need it.
- **Permissions stay minimal per job.** Only `architecture_coverage` needs
  `pull-requests: write` (PR comment); the others keep `contents: read`.

## Risks / Trade-offs

- [Duplicated checkout/restore work across more jobs increases total runner-minutes] →
  Acceptable: the issue explicitly states wall-clock PR feedback latency matters more than total
  runner minutes for this public repository, matching the #475 precedent.
- [Architecture Coverage job now builds the CLI/Testing projects itself instead of inheriting an
  in-job build from lint] → This is the one behavior-preserving-but-mechanically-different piece;
  mitigated by mirroring the exact build steps `architecture-coverage-report` already uses locally,
  and by running the split workflow end-to-end (actionlint/zizmor/Prettier locally, then a real PR
  run) before merging.
- [Losing the informal single "Pull Request Validation" required check name affects branch
  protection] → Explicitly deferred to the follow-up governance task per the issue's non-goals; no
  ruleset changes are made here.

## Migration Plan

Pure CI-configuration change with no runtime migration. Land as a single PR; branch protection
rulesets are updated separately once the new check names are confirmed stable across a few PR
runs (out of scope here).

## Open Questions

None — the job boundaries, `needs:` policy, and preserved-signal list are fully specified by the
issue's acceptance criteria.
