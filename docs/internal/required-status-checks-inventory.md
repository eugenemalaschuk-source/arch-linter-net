# Required status checks inventory (issue #479)

Prepared for the manual `Main` branch ruleset change that issue #479 depends on. Local
validation only becomes safe to relax (see
[feature-implementation-workflow.md](../ai/feature-implementation-workflow.md)) once PR CI is
the enforced, authoritative merge gate. As of this writing the active `Main` ruleset
(`gh api repos/eugenemalaschuk-source/arch-linter-net/rulesets/18790669`) has **no
`required_status_checks` rule at all**, so applying one is a real quality precondition, not a
formality.

Applying this change to the `Main` ruleset is a **manual step** (repository settings, not a
file in this repo) — this document exists so that step can be taken from an exact, verified
list instead of guesswork.

**This is a blocker, not a follow-up.** Issue #479 makes CI enforcement an explicit acceptance
criterion for relaxing mandatory local `make acceptance`, not an optional next step: *"The
final stabilized authoritative CI checks are required by the active `Main` ruleset, so relaxing
local validation does not weaken merge quality."* Until this step is applied, a red or missing
exhaustive CI check does not technically block a merge — the risk-based local-validation policy
in this PR should be treated as incomplete/not-yet-safe-to-rely-on until it is. Confirmed on
review of PR #490 (2026-08-12): the active ruleset still has no `required_status_checks` rule.

A ready-to-apply payload for this exact ruleset is at
[`main-ruleset-required-status-checks.json`](main-ruleset-required-status-checks.json)
(preserves every existing `Main` ruleset rule and adds the 15 contexts below). To apply it
(requires repository admin access):

```bash
gh api --method PUT repos/eugenemalaschuk-source/arch-linter-net/rulesets/18790669 \
  --input docs/internal/main-ruleset-required-status-checks.json
```

After applying, verify enforcement by confirming a PR with a red or missing required check is
blocked from merging (e.g. via the merge button's state on an open PR, or `gh pr view --json mergeStateStatus`).

## Source

Check names (`context` values, from the GitHub check-runs API) captured from the head commit of
PR [#489](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/489)
(`c772f102b7f31a294331efa8020fac6f3748eb70`), the most recently merged PR after the full CI
topology from #475 (E2E/packed-artifact isolation), #477 (parallel PR validation jobs), and #478
(Core unit sharding) stabilized in `.github/workflows/ci.yml`. Cross-checked against PR #484 and
the workflow source directly.

```
gh api repos/eugenemalaschuk-source/arch-linter-net/commits/<sha>/check-runs \
  -q '.check_runs[] | {name, app: .app.slug, conclusion}'
```

## Classification

### Authoritative merge blockers — required, run on every PR, no secrets needed

These come from jobs gated only on `if: github.event_name == 'pull_request'` with no
`TRUSTED_PR`/secret condition on their pass/fail outcome, plus the two non-`ci.yml` workflows
that don't touch secrets:

| Check (context) | Workflow | Notes |
|---|---|---|
| `Workflow Quality` | CI | actionlint/zizmor/Prettier on `.github/workflows` |
| `Repository Lint` | CI | `make lint` |
| `Coverage + Sonar` | CI | Always runs `make test-coverage` + `make test-tooling-coverage`; only the Sonar/Codecov *upload* steps inside this job are skipped for untrusted PRs (see `TRUSTED_PR` gate), so the job's own pass/fail is secret-independent |
| `Architecture Coverage` | CI | strict/audit self-architecture coverage gate |
| `Tooling / Support Tests` | CI | Python tooling test suites |
| `Unit Test Suite (Windows / Core Shard 1)` | CI | |
| `Unit Test Suite (Windows / Core Shard 2)` | CI | |
| `Unit Test Suite (Apple Silicon macOS / Core Shard 1)` | CI | |
| `Unit Test Suite (Apple Silicon macOS / Core Shard 2)` | CI | |
| `E2E Test Suite (Windows)` | CI | |
| `E2E Test Suite (Apple Silicon macOS)` | CI | |
| `Packed Artifact Test Suite (Windows)` | CI | |
| `Packed Artifact Test Suite (Apple Silicon macOS)` | CI | |
| `Analyze C#` | CodeQL | `github-actions` app, no secrets, `security-extended` queries |
| `Validate NuGet Packages` | Package Validation | `make pack` + package-set assertions, no secrets |

These 15 contexts are the recommended `required_status_checks` list.

### Informational / upload-only — should NOT be required

| Check (context) | App | Why not |
|---|---|---|
| `Main Badge Refresh` | github-actions | `if: github.event_name == 'push'` — only runs on pushes to `main`, never appears as a PR check |
| `CodeQL` | github-advanced-security | Summary/alerting check for the code-scanning results already produced by the `Analyze C#` job; alert-level enforcement for this is already handled by the ruleset's existing `code_scanning` rule (`security_alerts_threshold: medium_or_higher`), not by `required_status_checks` |

### Cannot be required — secret-backed, unavailable on fork/Dependabot PRs

| Check (context) | App | Why it can't be required |
|---|---|---|
| `SonarCloud Code Analysis` | sonarqubecloud | Posted by the SonarCloud GitHub App from the `coverage_sonar` job's Sonar begin/end steps, which run only when `TRUSTED_PR == 'true'` (i.e. `SONAR_TOKEN` is available). Fork and Dependabot PRs never get this check at all, so requiring it would permanently block them. |
| `codecov/patch` | codecov | Posted by the Codecov GitHub App from the `Upload test coverage to Codecov` step, also gated on `TRUSTED_PR == 'true'` (`CODECOV_TOKEN`). Same fork/Dependabot exposure problem. |

Per issue #479's non-goals, these are intentionally **not** substituted with a
secret-independent equivalent beyond what's already covered: the `Coverage + Sonar` job itself
(required above) already proves coverage collection succeeds without needing the external
Sonar/Codecov apps to report back.

Note the ruleset also already has `code_coverage` (`minimum_coverage: 80`) and `code_quality`
(`severity: warnings`) rule entries independent of `required_status_checks` — those existing
rule types are out of scope for this inventory and were not changed.

## Recommended `required_status_checks` contexts

```
Workflow Quality
Repository Lint
Coverage + Sonar
Architecture Coverage
Tooling / Support Tests
Unit Test Suite (Windows / Core Shard 1)
Unit Test Suite (Windows / Core Shard 2)
Unit Test Suite (Apple Silicon macOS / Core Shard 1)
Unit Test Suite (Apple Silicon macOS / Core Shard 2)
E2E Test Suite (Windows)
E2E Test Suite (Apple Silicon macOS)
Packed Artifact Test Suite (Windows)
Packed Artifact Test Suite (Apple Silicon macOS)
Analyze C#
Validate NuGet Packages
```

## Manual application step (not automated by this change)

See the blocker note near the top of this document for the exact `gh api` command and the
`main-ruleset-required-status-checks.json` payload. Applying it requires `admin`/ruleset-write
access on the repository and is intentionally left for a maintainer to run — mutating a
repository security setting is not something this workflow performs automatically.
