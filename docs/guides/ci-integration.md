# CI Integration

A good CI setup separates blocking strict validation from non-blocking audit visibility.

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

      - name: Build
        run: dotnet build --no-restore

      - name: Validate architecture (strict)
        run: dotnet arch-linter-net --mode strict --json > architecture-strict.json

      - name: Upload strict diagnostics
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-strict
          path: architecture-strict.json

      - name: Architecture audit report
        if: always()
        continue-on-error: true
        run: dotnet arch-linter-net --mode audit --json > architecture-audit.json

      - name: Upload audit diagnostics
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: architecture-audit
          path: architecture-audit.json
```

Use `dotnet tool restore` with a local tool manifest when the repository should pin the ArchLinterNet version. Use `dotnet tool install --global ArchLinterNet.Cli` only when global installation is acceptable for your pipeline.

## Exit code behavior

| Code | Meaning | CI action |
|------|---------|-----------|
| `0` | No selected contract violations | Pass |
| `1` | Validation completed and violations were found | Fail strict jobs; expected when manually inspecting failing audit rules |
| `2` | Invalid arguments, invalid configuration, missing files, or other runtime error | Fail closed |

See [Exit codes](../usage/exit-codes.md) for details.

## Strict vs audit jobs

Strict validation is the no-new-debt gate. It should fail a pull request when an enforced architecture boundary is violated.

Audit validation is visibility for migration work. It can be uploaded as an artifact, posted to a dashboard, or inspected periodically, but it should not accidentally become the strict gate unless the team intentionally promotes the audit rule.

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

## Baseline debt semantics in the coverage gate

When architecture coverage is wired into CI as a quality gate (see the `architecture-coverage` steps in this repository's `.github/workflows/ci.yml`, which run after the existing acceptance gate against the same already-built solution), baseline entries change how findings are reported, not whether they exist:

- **Existing accepted debt** lives in the baseline file and does not fail the pull request. The strict run still reports it in `coverage_findings`/`coverage_summary`, but a finding matched by a baseline entry is treated as known debt rather than a regression.
- **New coverage findings** — anything not matched by an existing baseline entry — fail the pull request. This is what keeps the gate "no new debt" instead of "no debt."
- **Resolved baseline entries** become stale: once the underlying violation no longer exists, the baseline entry has nothing left to match. Stale baseline entries should be removed during normal maintenance so the baseline file reflects only real outstanding debt.
- **Exclusions require a `reason`.** An exclusion is a deliberate, reviewed decision to leave a unit out of coverage scope — it is not a way to silently bypass the gate. Treat the `reason` field as required documentation, not boilerplate, and review exclusions the same way you'd review a baseline entry.

To inspect the same full-solution coverage report locally before pushing, run `make architecture-coverage-report`, which prints both the Markdown report (the same one posted to pull requests) and the raw JSON view.

**All-zero counts can mean two different things.** If `coverage_summary` is an empty list, the policy defines no coverage contracts at all (`strict_coverage`/`audit_coverage` are absent) — the report's note line calls this out explicitly. That is different from a policy that *does* define coverage contracts and reports zero uncovered/stale/unknown items, which means real coverage contracts exist and nothing is currently failing them. This repository's own `architecture/dependencies.arch.yml` defines an `assembly`-scope and a `namespace`-scope `strict_coverage` contract covering all four first-party assemblies and their root namespaces, so the gate reflects real coverage rather than an empty, trivially-passing policy.

## Test coverage with Codecov and SonarCloud

This repository treats line test coverage and architecture coverage as two separate CI signals:

- `make test-coverage` runs the NUnit test projects with `XPlat Code Coverage`, writes Cobertura XML for Codecov, writes OpenCover XML for SonarCloud, and emits TRX test result files under `test-results/`.
- `make architecture-coverage-report` evaluates ArchLinterNet coverage contracts and prints architecture-specific Markdown + JSON diagnostics.

The CI workflow runs `make test-coverage` after the acceptance gate, resolves the generated `coverage.cobertura.xml` files, uploads them with `codecov/codecov-action@v5`, and points SonarScanner for .NET at the generated `coverage.opencover.xml` and `.trx` files before ending the SonarCloud analysis. The chosen Codecov authentication mode for this repository is the repository secret `CODECOV_TOKEN`, not OIDC.

To inspect the same test-coverage input locally before pushing, run:

```bash
make test-coverage
make test-coverage-badge
```

The first command regenerates the raw Cobertura XML reports, OpenCover XML reports, and TRX files in `test-results/`. The second command merges the Cobertura reports locally and prints the same overall line-coverage percentage that the README badge is expected to reflect once Codecov ingests the upload from `main`.

### Codecov auth and fork behavior

The upload step uses `CODECOV_TOKEN` from GitHub Actions secrets. Secrets are available for pushes to this repository and for pull requests whose head branch also lives in this repository, but not for untrusted fork pull requests.

That is why the workflow gates upload with:

```yaml
if: github.event_name == 'push' || github.event.pull_request.head.repo.full_name == github.repository
```

Fork PRs still run the normal acceptance and architecture checks, but they skip the Codecov upload because GitHub does not expose repository secrets to untrusted fork workflows.

### Failure mode expectations

Codecov upload is intentionally configured with `fail_ci_if_error: false`. Test execution remains required, but transient Codecov or network issues should not make an otherwise healthy pull request flaky.

## SonarCloud pull request analysis

The same `ci.yml` `validate` job also runs SonarCloud analysis for `main` pushes and trusted pull requests from branches in this repository:

- The workflow checks out the repository with `fetch-depth: 0` so SonarCloud can compare a pull request branch against its base branch.
- The scanner waits for the SonarCloud quality gate result, so the workflow fails when the Sonar quality gate fails.
- For pull requests, the workflow publishes a job summary link to `https://sonarcloud.io/summary/new_code?id=<project-key>&pullRequest=<number>` so reviewers have a direct path to the SonarCloud PR analysis in addition to the GitHub PR decoration/check created by SonarCloud.
- The gate is evaluated on new code introduced by the PR, as configured by SonarCloud for pull-request analysis.

### Required GitHub configuration

The repository workflow expects:

- `SONAR_TOKEN` GitHub Actions secret for SonarCloud authentication.
- Optional `SONAR_PROJECT_KEY` repository variable. If unset, the workflow uses the public project key `eugenemalaschuk-source_arch-linter-net`.
- Optional `SONAR_ORGANIZATION` repository variable. If unset, the workflow uses the public organization key `eugenemalaschuk-source`.

If a trusted push or same-repository pull request is missing required SonarCloud configuration, the workflow fails with an explicit diagnostic instead of silently skipping analysis.

### Fork pull requests

GitHub does not expose repository secrets to untrusted fork pull requests. For that reason, fork PRs do not run the trusted SonarCloud analysis path from this repository workflow. The job summary explains that the SonarCloud PR gate was skipped for that fork run, while same-repository PRs remain fail-closed.

### Automatic analysis caveat

The current public SonarCloud project metadata indicates that automatic analysis is enabled. For CI-based analysis with coverage import and PR quality-gate enforcement to be the source of truth, maintainers should confirm the project is using the intended CI-based analysis mode in SonarCloud and disable automatic analysis there if it would otherwise compete with the GitHub Actions scan.

### Recommended required check

After the first successful decorated pull request run, configure GitHub branch protection manually to require the Sonar-created PR status/check for this repository. For this repository's validated PR flow, GitHub currently renders that check as `SonarCloud Code Analysis`, but maintainers should still verify the exact displayed check name in GitHub before making it required.

### Post-merge verification

After merging the workflow change:

- open or update a same-repository test pull request;
- confirm GitHub shows the SonarCloud PR status/check and decoration;
- confirm the workflow summary link opens the SonarCloud PR analysis page;
- confirm the `main` project page updates at `https://sonarcloud.io/summary/overall?id=eugenemalaschuk-source_arch-linter-net&branch=main`.

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

Repository release automation may publish MkDocs to GitHub Pages, but PR CI should only validate docs and code. It must not publish packages, create releases, or deploy documentation.
