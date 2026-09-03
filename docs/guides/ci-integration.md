# CI Integration

A CI workflow should choose its mode boundary deliberately. When one workflow requires
both strict and audit results from the same build state, use the combined
`--mode strict,audit --ensure-built` invocation: it owns one immutable analysis
snapshot and one snapshot-owned build/preflight preparation (including any
post-build receipt verification), then evaluates both modes from that snapshot.
The combined command fails when either requested mode fails. When audit is
intentionally advisory, retain separate strict-blocking and non-blocking-audit
steps instead; those independent CLI processes do not reuse one another's
prepared state.

The provider-neutral 0.5.1 contract, offline schema commands, sequential mode,
and safe POSIX/PowerShell/Make/Task/Tilt templates are in [0.5.1 reference
entrypoints](reference-entrypoints.md). GitHub Actions below is one example
provider, not a product dependency.

## Recommended GitHub Actions workflow

```yaml
name: Architecture validation

on:
  pull_request:
  push:
    branches: [main]

jobs:
  architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore dependencies
        run: dotnet restore

      - name: Validate architecture (strict + audit)
        run: |
          dotnet arch-linter-net --mode strict,audit --ensure-built --no-restore \
            --report json=architecture-results.json \
            --report sarif=architecture-results.sarif

      - name: Upload architecture diagnostics
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-results
          path: |
            architecture-results.json
            architecture-results.sarif
```

Use `dotnet tool restore` with a local tool manifest when the repository should pin the ArchLinterNet version. Use `dotnet tool install --global ArchLinterNet.Cli` only when global installation is acceptable for your pipeline.

The example above is provider-neutral guidance, not a requirement to rerun the
same validation twice. ArchLinterNet's own repository uses a stricter protected
PR gate and does **not** repeat its full lint/architecture/cross-platform matrix
after merge. Its `main` push is reserved for fresh coverage/Sonar/Codecov
telemetry plus an independent installable `main.N` package build.

## Exit code behavior

| Code | Meaning | CI action |
|------|---------|-----------|
| `0` | No selected contract violations | Pass |
| `1` | Validation completed and violations were found | Fail strict jobs; expected when manually inspecting failing audit rules |
| `2` | Invalid arguments, invalid configuration, missing files, or other runtime error | Fail closed |

For a combined `strict,audit` command, code `1` is the aggregate result: either
requested mode failing makes the command fail. The JSON and SARIF files above
contain the completed result for each mode; report routing renders those
outcomes and does not run analysis again.

See [Exit codes](../usage/exit-codes.md) for details.

## Architecture Health badge payload

```bash
arch-linter-net badge architecture-health \
  --input architecture-health.json \
  --output architecture-health-badge.json
```

This is a local projection over the canonical Health artifact and its canonical
policy-inventory receipt. The primary message contains Health, accumulated
explicit ignores, and effective policy controls. A rule count is transparency
about configured controls, not a coverage percentage or quality score. Health,
ignore debt, rule count, and colors belong to the CLI; CI only transports the
complete generated JSON.

For this repository, the required read-only PR Architecture Coverage job emits
the exact payload with a bounded manifest binding repository, PR/base context,
head SHA, head Git-tree identity, producer run, byte count, and SHA-256. A
trusted `push main` publisher resolves the merged PR and promotes that payload
only if the validated PR tree equals the merged `main` tree. This tree proof is
required for squash merge: matching commit SHA alone is not sufficient.

If the PR, required producer, artifact, manifest, hash, or tree proof is
missing, stale, failed, expired, ambiguous, or invalid, the publisher replaces
the fixed public endpoint with the CLI-generated `UNASSESSABLE · ? ignores · ? rules` payload and publication metadata. It does not reuse a prior healthy
payload as current, rerun architecture analysis, mutate policy/baselines, or
deploy MkDocs/GitHub Pages. The public endpoint is a fixed raw JSON file on an
automation-owned static branch, suitable for Shields' `endpoint` image.

## Legacy architecture-policy badge payload

`arch-linter-net badge architecture-policy --input architecture-strict.json`
projects strict validation JSON into a Shields endpoint payload without rerunning analysis.
It returns `0` with `passing`/`brightgreen`, `1` with `failing`/`red`, and `2` with
`unavailable`/`red`. A workflow can use its exit status as the blocking gate while a
badge service consumes the JSON endpoint.

`arch-linter-net coverage report --input architecture-strict.json --output architecture-coverage.md`
remains the standalone coverage projection. It is useful for a coverage artifact or local review,
but it is not the repository's pull-request comment; use `--max-failure-diagnostics 3` for a
compact coverage view and pass `--changed-files`, `--repo-root`, and `--diff-status failed` when
applicable.

## Strict vs audit jobs

Strict validation is the no-new-debt gate. It should fail a pull request when an enforced architecture boundary is violated.

Audit validation is visibility for migration work. It can be uploaded as an artifact, posted to a dashboard, or inspected periodically, but it should not accidentally become the strict gate unless the team intentionally promotes the audit rule.

If audit is intentionally advisory, keep the backward-compatible two-step
workflow and make only the audit step non-blocking:

```yaml
- name: Validate architecture (strict)
  run: |
    dotnet arch-linter-net --mode strict --ensure-built --no-restore \
      --report json=architecture-strict.json

- name: Architecture audit report
  if: always()
  continue-on-error: true
  run: |
    dotnet arch-linter-net --mode audit --ensure-built --no-restore \
      --report json=architecture-audit.json
```

Each step is a separate CLI process with its own preparation. Choose this
alternative when audit findings should remain visible without contributing to
the blocking decision; choose the combined invocation when both mode results
must be required from one build-state snapshot.

## Baseline in CI

For existing repositories with known debt:

```yaml
- name: Validate architecture with baseline
  run: dotnet arch-linter-net \
    --policy architecture/dependencies.arch.yml \
    --baseline architecture/baseline.arch.yml \
    --mode strict
```

The baseline should be reviewed like code and cleaned up as violations are fixed.

### New-debt gate with policy-weakening guardrails

Use `gate` when CI needs one read-only decision over both exact reviewed
persistent debt and the separate change-time policy-weakening guardrail. It is
not a third validation mode: `strict` and `audit` retain their usual meanings,
and `--mode all` merely collects complete candidates from both existing modes.

```yaml
- name: Export base policy context
  run: git worktree add --detach .ci-base origin/main && dotnet arch-linter-net policy context --policy .ci-base/architecture/dependencies.arch.yml --format json > base-policy-context.json

- name: Export current policy context
  run: dotnet arch-linter-net policy context --policy architecture/dependencies.arch.yml --format json > current-policy-context.json

- name: Reject new architecture debt and policy weakening
  run: dotnet arch-linter-net gate \
    --policy architecture/dependencies.arch.yml \
    --baseline architecture/baseline.arch.yml \
    --base-context base-policy-context.json \
    --current-context current-policy-context.json \
    --format json > architecture-debt-gate.json
```

The base context must be exported from the base policy state, not reloaded from
the current checkout. The gate returns `1` for a new, resolved, stale,
configuration-error, or ambiguous persistent-debt comparison and for an
`error` policy-weakening finding. `warn` and `impact_not_proven` weakening
records remain visible without becoming baseline debt. It returns `2` for
missing/incomplete inputs or blocked complete analysis; CI must fail closed.

`gate` never writes a baseline. Use `baseline diff`, `update`, or `prune` in a
separate reviewed maintenance change.

### CI reads baselines; it never writes them

CI runs only the read-only baseline commands:

```yaml
- name: Verify the baseline is still in sync
  run: dotnet arch-linter-net baseline verify \
    --policy architecture/dependencies.arch.yml \
    --baseline architecture/baseline.arch.yml
```

`baseline verify` exits non-zero when the baseline has drifted — stale entries whose violation is
gone, entries that now match more than one violation, or entries naming a contract the policy no
longer has. `baseline diff` reports the same comparison without gating.

Do **not** wire `baseline generate`, `baseline update`, `baseline prune`, or `baseline migrate` into
a workflow that runs on every push, and do not commit their output automatically. A baseline is a
record of debt somebody accepted; a job that regenerates it turns every new violation into
pre-approved debt and removes the review step the file exists to create. Run those commands locally,
review the diff, and commit it like any other change. `--dry-run` prints exactly what would change,
which is the form worth pasting into a pull request description.

If you want CI to *notice* that a baseline is out of date rather than fix it, add
`baseline verify` as above, or `baseline update --dry-run --json` as a reporting step whose output is
uploaded as an artifact — neither writes a file.

## Baseline debt semantics in the coverage gate

When architecture coverage is wired into CI as a quality gate (the repository's read-only
architecture report producer runs on the protected pull-request candidate), baseline entries
change how findings are reported, not whether they exist:

- **Existing accepted debt** lives in the baseline file and does not fail the pull request. The strict run still reports it in `coverage_findings`/`coverage_summary`, but a finding matched by a baseline entry is treated as known debt rather than a regression.
- **New coverage findings** — anything not matched by an existing baseline entry — fail the pull request. This is what keeps the gate "no new debt" instead of "no debt."
- **Resolved baseline entries** become stale: once the underlying violation no longer exists, the baseline entry has nothing left to match. Stale baseline entries should be removed during normal maintenance so the baseline file reflects only real outstanding debt.
- **Exclusions require a `reason`.** An exclusion is a deliberate, reviewed decision to leave a unit out of coverage scope — it is not a way to silently bypass the gate. Treat the `reason` field as required documentation, not boilerplate, and review exclusions the same way you'd review a baseline entry.

To inspect the full-solution coverage report locally before pushing, run
`make architecture-coverage-report`; it prints the standalone coverage Markdown and raw JSON
view. The unified pull-request report is a separate Core/CLI projection over compatible Health and
architecture-change artifacts.

## Secure unified Architecture PR report publication

The repository renders the reviewer-facing architecture PR report with
`arch-linter-net report pr` before any comment is written. The pull-request workflow has only
read permission: it uploads the exact Markdown plus a bounded manifest that binds the report to
the repository, PR number, head SHA, CI run and attempt, report schema/kind/marker, byte count,
and SHA-256.

A separate completed-CI publisher is the only job with pull-request write permission. It performs
no checkout and treats downloaded report bytes as inert data. Before updating the one sticky
comment it verifies the current PR head, producer run identity, exact artifact shape, bounded
sizes, manifest fields, and report hash. It neither reconstructs Architecture Health nor adds
build, test, quality-service, or security-service status.

This separation also applies to fork and Dependabot pull requests: their producer can execute with
read-only permissions, while the publisher never checks out or executes fork-controlled source or
artifact content. If a report is missing, cancelled, stale, malformed, or exceeds the transport
limit, publication fails closed and can show only a fixed integration-unavailable message. It never
reuses an older green report as evidence for a new head. The raw strict/audit/coverage artifacts
and the standalone coverage command remain available for drill-down.

**All-zero counts can mean two different things.** If `coverage_summary` is an empty list, the policy defines no coverage contracts at all (`strict_coverage`/`audit_coverage` are absent) — the report's note line calls this out explicitly. That is different from a policy that *does* define coverage contracts and reports zero uncovered/stale/unknown items, which means real coverage contracts exist and nothing is currently failing them. This repository's own `architecture/dependencies.arch.yml` defines `assembly`-, `project`-, `namespace`-, and `rule_input`-scope `strict_coverage` contracts covering all four first-party assemblies, every discovered production project, their root namespaces, and the rule inputs of its source-sensitive strict rules, so the gate reflects real coverage rather than an empty, trivially-passing policy.

## Repository badge policy

ArchLinterNet's README deliberately distinguishes merge authority from
post-merge telemetry:

- **Main quality** is the GitHub Actions badge for `main-quality.yml` on the
  merged `main` branch. It means the current merged revision completed the
  Linux coverage telemetry pipeline and successfully refreshed the external
  quality services.
- **Test coverage** is the Codecov badge explicitly scoped to `branch=main`.
  It is refreshed by the same post-merge coverage reports.
- **Sonar Quality Gate / Maintainability / Reliability / Security** are direct
  SonarCloud project badges for `branch=main`; the main telemetry workflow sends
  OpenCover/TRX plus Python coverage before ending the scanner.
- **Architecture Health** is a canonical ArchLinterNet badge, not a workflow
  status. It contains Health, explicit ignore debt, and effective policy
  controls from required PR evidence only after exact merged-tree proof. Its
  unassessable state is explicit when that promotion proof is unavailable.

The repository's full self-policy and architecture-coverage validation remain
required PR checks. They are not repeated after merge merely to refresh generic
quality badges.

## Test coverage with Codecov and SonarCloud

This repository treats line test coverage and architecture coverage as two separate CI signals:

- `make test-coverage` runs the NUnit unit bucket with `XPlat Code Coverage`, writes Cobertura XML for Codecov, writes OpenCover XML for SonarCloud, and emits TRX test result files under `test-results/`.
- `make architecture-coverage-report` evaluates ArchLinterNet coverage contracts and prints architecture-specific Markdown + JSON diagnostics.

The required PR workflow uses three isolated Linux coverage shards and aggregates
them into the PR Sonar/Codecov path. After merge, `main-quality.yml` runs the
same coverage shard targets independently of the full PR validation matrix,
downloads the reports, collects Python tooling coverage, uploads Cobertura to
Codecov, and ends a SonarCloud `main` analysis that imports the OpenCover/TRX and
Python coverage data.

That post-merge run is what keeps the README's `Main quality`, Codecov, and
SonarCloud main-branch badges current for the merged revision.

To inspect the same test-coverage input locally before pushing, run:

```bash
make test-coverage
make test-coverage-badge
```

The first command regenerates the raw Cobertura XML reports, OpenCover XML reports, and TRX files in `test-results/`. The second command merges the Cobertura reports locally and prints the same overall line-coverage percentage that the README badge is expected to reflect once Codecov ingests the upload from `main`.

### Codecov auth and fork behavior

The upload steps use `CODECOV_TOKEN` from GitHub Actions secrets.

- Trusted same-repository PRs may upload PR coverage; fork PRs still run the
  coverage tests but skip secret-backed uploads because GitHub does not expose
  repository secrets to untrusted forks.
- The `main-quality.yml` push runs on the protected repository branch and has
  access to the existing repository secret, so it uploads the authoritative
  merged-main coverage.

No additional secret is required by the `main.N` package workflow; GitHub
Packages uses its job-scoped built-in `GITHUB_TOKEN` instead.

### Failure mode expectations

The two coverage contexts deliberately have different external-service failure
semantics:

- PR coverage execution remains required; the existing PR Codecov upload is
  best-effort so a transient Codecov outage does not make an otherwise valid PR
  flaky.
- The post-merge `Main Quality Telemetry` upload is fail-closed for Codecov and
  SonarCloud. If either external refresh cannot complete, the `Main quality`
  workflow badge is red instead of falsely implying that the external badges
  were updated for the merged revision.

## SonarCloud analysis

SonarCloud has separate PR and merged-main roles.

### Pull requests

The `ci.yml` coverage/Sonar job runs SonarCloud analysis for trusted pull
requests from branches in this repository:

- The workflow checks out the repository with `fetch-depth: 0` so SonarCloud can compare a pull request branch against its base branch.
- The scanner waits for the SonarCloud quality gate result, so the workflow fails when the Sonar quality gate fails.
- The workflow publishes a job summary link to `https://sonarcloud.io/summary/new_code?id=<project-key>&pullRequest=<number>` so reviewers have a direct path to the SonarCloud PR analysis in addition to the GitHub PR decoration/check created by SonarCloud.
- The gate is evaluated on new code introduced by the PR, as configured by SonarCloud for pull-request analysis.

### Merged `main`

`main-quality.yml` is the only ordinary post-merge Sonar path. It does not rerun
repository lint, architecture validation, Windows/macOS test matrices, E2E, or
packed-artifact acceptance. It runs the Linux coverage shards needed to produce
fresh coverage evidence, performs the Sonar build inside the scanner context,
imports .NET/Python coverage, and ends the scanner on the merged `main` commit.

The main scan is fail-closed in that workflow: a missing token, failed coverage
shard, failed Codecov upload, scanner failure, or failed Sonar quality gate makes
the `Main quality` workflow red. That failure is telemetry about an already
merged revision; it does not retroactively weaken or bypass the PR merge gate.

### Required GitHub configuration

The repository workflow expects:

- `SONAR_TOKEN` GitHub Actions secret for SonarCloud authentication.
- `CODECOV_TOKEN` GitHub Actions secret for Codecov authentication.
- Optional `SONAR_PROJECT_KEY` repository variable. If unset, the workflow uses the public project key `eugenemalaschuk-source_arch-linter-net`.
- Optional `SONAR_ORGANIZATION` repository variable. If unset, the workflow uses the public organization key `eugenemalaschuk-source`.

These are the existing quality-service credentials; #707 does not introduce a
new repository secret for main package publication.

If a trusted same-repository PR or `main` telemetry run is missing required
SonarCloud configuration, the relevant workflow fails with an explicit
diagnostic instead of silently claiming a completed scan.

### Fork pull requests

GitHub does not expose repository secrets to untrusted fork pull requests. For that reason, fork PRs do not run the trusted SonarCloud analysis path from this repository workflow. The job summary explains that the SonarCloud PR gate was skipped for that fork run, while same-repository PRs remain fail-closed.

### Automatic analysis caveat

The current public SonarCloud project metadata indicates that automatic analysis is enabled. For CI-based analysis with coverage import and PR quality-gate enforcement to be the source of truth, maintainers should confirm the project is using the intended CI-based analysis mode in SonarCloud and disable automatic analysis there if it would otherwise compete with the GitHub Actions scan.

### Recommended required check

After the first successful decorated pull request run, configure GitHub branch protection manually to require the Sonar-created PR status/check for this repository. For this repository's validated PR flow, GitHub currently renders that check as `SonarCloud Code Analysis`, but maintainers should still verify the exact displayed check name in GitHub before making it required.

### Post-merge verification

After merging a CI topology change:

- confirm `Main Quality Telemetry` ran for the merged `main` SHA;
- confirm its three Linux coverage shards completed and the aggregate job is green;
- confirm the Codecov repository page and README coverage badge show `main` data from the merged revision;
- confirm the SonarCloud `main` page and project badges refresh for the merged revision;
- confirm the ordinary `CI` workflow, CodeQL push job, Windows/macOS matrices,
  architecture coverage and packed-artifact acceptance did not rerun merely
  because of the merge.

## Azure Pipelines example

```yaml
- task: DotNetCoreCLI@2
  displayName: Restore local tools
  inputs:
    command: custom
    custom: tool
    arguments: restore

- script: dotnet arch-linter-net --mode strict
  displayName: Validate architecture
```

## Documentation publication note

PR CI and both ordinary `main` workflows may validate or reference documentation
sources, but they never deploy MkDocs. GitHub Pages deployment remains owned by
`release-nuget.yml` and runs only when the maintainer explicitly starts a real
public release with `publish: true`.
