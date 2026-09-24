# ArchLinterNet repository CI

This page describes the CI of the **ArchLinterNet source repository**. It is a
reference example, not infrastructure that another repository must reproduce.
For consumer onboarding, start with [Run in CI](../guides/ci-integration.md)
and [badge adoption](../guides/badge-adoption.md).

## Workflow responsibilities

| Lane | Responsibility |
| --- | --- |
| [PR CI](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/ci.yml) | Required lint, architecture, cross-platform tests, coverage, and PR evidence. |
| [PR report publisher](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/publish-architecture-pr-report.yml) | Verify the read-only producer's bounded artifact and update one sticky comment; do not execute PR code. |
| [Health badge publisher](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/publish-architecture-health-badge.yml) | Promote accepted PR evidence to the public raw snapshot after exact merged-tree verification. |
| [Main quality telemetry](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/main-quality.yml) | Produce current-revision coverage and deliver/verify SonarCloud and Codecov telemetry. |
| [Main packages](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/main-packages.yml) | Publish development/dogfood `main.N` packages, not official release authority. |
| [Release](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/.github/workflows/release-nuget.yml) | Validate the selected immutable candidate; authorized publication includes packages, GitHub Release, and Pages documentation. |

The complete PR architecture/test matrix is not repeated after every merge
merely to refresh badges. Main coverage tests are a distinct telemetry input;
that does not make the telemetry job a second architecture evaluator.

## Architecture Health publication

The required PR Architecture Coverage producer creates the canonical payload
and a bounded manifest. The trusted `push: main` publisher resolves the merged
PR and verifies the producer workflow/check, run/attempt, artifact bytes,
digests, and equality of the analyzed PR tree with the accepted main tree.
Missing, ambiguous, stale, failed, or invalid evidence yields an explicit
unassessable publication, not a recycled healthy result.

The destination is the public `architecture-health-badge` branch, **not**
`main` or the Pages site. This is the `github-raw` static-snapshot path; it is
separate from both experimental Relay and consumer-owned direct Workers.

The README image links to the
[canonical v2 publication receipt](https://raw.githubusercontent.com/eugenemalaschuk-source/arch-linter-net/architecture-health-badge/architecture-health-publication.json).
Compare its analyzed/base/head and merged-main identities, producer/publisher
run attempts, publication status/reason, and payload digest with the
[raw Health badge JSON](https://raw.githubusercontent.com/eugenemalaschuk-source/arch-linter-net/architecture-health-badge/architecture-health.json).
Only then inspect Shields and GitHub's README image proxy (Camo).

A new receipt may be current while the semantic headline is unchanged. A cached
image is not evidence of current publication. Unlike an expiry-enforcing Relay
or direct origin, a raw file does not become unavailable on read when the
publisher stops. This README image is a snapshot compatibility view, not an
instantaneous current-main guarantee.

[Repository metrics](repository-metrics.md) have separate absolute snapshot
badges and PR deltas. They do not add fields to the Health badge, redefine its
gate, or turn diagnostic metrics into new blocking rules.

## Quality telemetry

README signals answer different questions:

| Signal | Meaning |
| --- | --- |
| Main quality | The merged revision's coverage and telemetry delivery completed and were verified. |
| Test coverage | Codecov line coverage, explicitly scoped to `main`. |
| Sonar Quality Gate / Maintainability / Reliability / Security | SonarCloud's direct project assessment of `main`. |
| Architecture Health | The CLI's architecture assessment from verified evidence; not test coverage or workflow status. |

The PR workflow and main telemetry lane use three Linux coverage shards. The
main lane produces a complete current-SHA coverage receipt, imports
OpenCover/TRX and Python coverage into SonarCloud, and uploads Cobertura to
Codecov. It does not rerun the full architecture, Windows/macOS, E2E, or
packed-artifact matrix.

A successfully processed red Sonar quality gate is visible as a warning and
through the direct Sonar badge/dashboard. It need not make **delivery** fail.
Missing credentials, incomplete coverage, scanner/upload/processing failure,
wrong revision, missing coverage import, unknown Sonar status, or failed
Codecov delivery do fail the main telemetry workflow. This distinction never
weakens the required PR gate.

### Configuration and fork behavior

The source repository uses `SONAR_TOKEN` and `CODECOV_TOKEN` for these external
services. `SONAR_PROJECT_KEY` and `SONAR_ORGANIZATION` can override the
workflow's project defaults. These are not ArchLinterNet CLI requirements.
Check the service's intended CI-based analysis mode; do not assume an account
setting from documentation or let a competing automatic scan supply the wrong
revision's result.

Trusted same-repository PRs run the Sonar analysis and wait for its quality
gate. PR Codecov delivery is best-effort; coverage execution remains required.
Fork PRs run the applicable read-only checks without secret-backed uploads.
Missing required configuration for a trusted analysis is not silently treated
as a successful scan. Verify the exact Sonar-created GitHub check name before
making it required in branch protection.

### Local diagnostics

```bash
make test-coverage
make test-coverage-badge
make architecture-coverage-report
```

The first two commands inspect line coverage and the merged local Cobertura
percentage. The third inspects architecture coverage. They are different
signals; the standalone architecture coverage Markdown is not the unified PR
comment rendered by `report pr`.

### Verify a CI topology change

Inspect the merged SHA's main telemetry run, complete coverage inventory,
Sonar analysis revision/imports, and Codecov upload. Check the direct service
badges separately from the workflow badge. Also confirm the full PR matrix did
not run again simply because a merge occurred.

## Documentation publication

`make lint-docs` validates the documentation; `make docs-build` renders it.
Neither publishes it. The release workflow's `deploy-docs` job runs only for
an authorized publication with `publish: true`. An ordinary merge updates the
source documentation, not the live Pages site.

A documentation correction is visible on Pages only after that publication
step succeeds. Follow the [release process](release-process.md); a docs PR
must not quietly add a new main-branch deployment or manufacture a release.
