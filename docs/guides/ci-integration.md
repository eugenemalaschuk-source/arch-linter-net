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

## Architecture-policy badge payload

`arch-linter-net badge architecture-policy --input architecture-strict.json`
projects strict validation JSON into a Shields endpoint payload without rerunning analysis.
It returns `0` with `passing`/`brightgreen`, `1` with `failing`/`red`, and `2` with
`unavailable`/`red`. A workflow can use its exit status as the blocking gate while a
badge service consumes the JSON endpoint.

For a pull-request coverage comment, render the strict JSON with
`arch-linter-net coverage report --input architecture-strict.json --output architecture-coverage.md`.
Use `--max-failure-diagnostics 3` for the compact comment and pass `--changed-files`,
`--repo-root`, and `--diff-status failed` when applicable.

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

When architecture coverage is wired into CI as a quality gate (see the `architecture-coverage` steps in this repository's `.github/workflows/ci.yml`, which run after the existing acceptance gate against the same already-built solution), baseline entries change how findings are reported, not whether they exist:

- **Existing accepted debt** lives in the baseline file and does not fail the pull request. The strict run still reports it in `coverage_findings`/`coverage_summary`, but a finding matched by a baseline entry is treated as known debt rather than a regression.
- **New coverage findings** — anything not matched by an existing baseline entry — fail the pull request. This is what keeps the gate "no new debt" instead of "no debt."
- **Resolved baseline entries** become stale: once the underlying violation no longer exists, the baseline entry has nothing left to match. Stale baseline entries should be removed during normal maintenance so the baseline file reflects only real outstanding debt.
- **Exclusions require a `reason`.** An exclusion is a deliberate, reviewed decision to leave a unit out of coverage scope — it is not a way to silently bypass the gate. Treat the `reason` field as required documentation, not boilerplate, and review exclusions the same way you'd review a baseline entry.

To inspect the same full-solution coverage report locally before pushing, run `make architecture-coverage-report`, which prints both the Markdown report (the same one posted to pull requests) and the raw JSON view.

**All-zero counts can mean two different things.** If `coverage_summary` is an empty list, the policy defines no coverage contracts at all (`strict_coverage`/`audit_coverage` are absent) — the report's note line calls this out explicitly. That is different from a policy that *does* define coverage contracts and reports zero uncovered/stale/unknown items, which means real coverage contracts exist and nothing is currently failing them. This repository's own `architecture/dependencies.arch.yml` defines `assembly`-, `project`-, `namespace`-, and `rule_input`-scope `strict_coverage` contracts covering all four first-party assemblies, every discovered production project, their root namespaces, and the rule inputs of its source-sensitive strict rules, so the gate reflects real coverage rather than an empty, trivially-passing policy.

## Architecture policy badge

The README's **Architecture policy** badge is a dynamic GitHub Actions status
badge for the latest `main` run of
[Architecture Policy](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/architecture-policy.yml).
That workflow runs the repository's authoritative read-only strict self-policy
gate:

```bash
make restore
make lint-architecture
```

It is intentionally a status signal, not an architecture-coverage percentage or
a unit-test coverage metric. Architecture coverage remains the strict/audit
JSON and Markdown report described above; test coverage remains the separate
Codecov signal below. The workflow has read-only contents permission, writes no
badge files or README commits, and does not publish packages, releases, or
GitHub Pages content.

After a merge, GitHub refreshes the badge from the next `main` workflow run. If
the badge is failing or stale, inspect that run's **Strict Self-Policy** job:
the `make lint-architecture` output names the violated policy rule. Run the two
commands above locally after restoring dependencies to reproduce the result.

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
