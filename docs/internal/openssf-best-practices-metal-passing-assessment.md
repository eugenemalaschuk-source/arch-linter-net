# OpenSSF Best Practices — Metal-series `passing` assessment

Tracks: #287. Related: #286 (badge display, closed), #257 (trust badge story).

Badge series: **Metal-series `passing`** (the classic Best Practices
passing/silver/gold questionnaire), project `13572`. This is explicitly
**not** the separate OpenSSF Baseline series (`baseline-1/2/3`), which is a
different questionnaire on the same site and is out of scope for this
assessment — see the issue's "Target badge series" note.

Project record: <https://www.bestpractices.dev/en/projects/13572>

Assessment date: 2026-08-23. Observed live status at that date: **84%**
against the passing-level criteria. The site's own automated project-page
percentage may drift as later answers are submitted; this document is
point-in-time evidence, not a live mirror.

## How to use this document

This is repository-safe preparation evidence for the maintainer's manual
questionnaire submission on bestpractices.dev (see "Manual step" below). It
is not itself the questionnaire. Answers below are conservative: a
criterion is marked evidence-backed only where current repository state
genuinely satisfies it, using the project's actual solo-maintainer,
AI-assisted, early-preview operating model — no invented community size,
SLA, or support commitment.

## Criteria assessed as unmet at the start of this task

The site listed the following unmet/needs-justification criteria at
assessment time. Each is resolved below with an answer and evidence link,
or an explicit reason it stays unmet.

| Criterion ID | Category | Type | Resolution |
|---|---|---|---|
| `contribution_requirements` | Basics | SHOULD | **Fixed** — added [CONTRIBUTING.md](../../CONTRIBUTING.md) documenting coding conventions, formatting, warnings-as-errors, tests, and architecture-governance requirements for contributions. |
| `release_notes` | Change Control | MUST | **Met** — every GitHub Release carries categorized, human-readable notes (Breaking Changes / Features / Fixes / Documentation / CI-CD / Dependencies / Other Changes) generated from PR titles per `.github/release.yml`; not raw version-control logs. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/releases>. |
| `release_notes_vulns` | Change Control | MUST | **N/A** — no publicly known runtime vulnerability with a CVE (or similar) has been fixed in any release; there is nothing to identify. Confirmed no security advisories exist: `gh api repos/eugenemalaschuk-source/arch-linter-net/security-advisories` returns `[]`. |
| `report_responses` | Reporting | MUST | **Met** — the project is solo-maintainer and early-preview; all issues to date are self-filed by the maintainer and are triaged/closed via a linked pull request (e.g. #491, #486). There have been no external bug reports in the 2–12 month window; none are outstanding without a response. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/issues?q=is%3Aissue+label%3Abug>. |
| `enhancement_responses` | Reporting | SHOULD | **Met** — same evidence pattern as `report_responses`: enhancement-shaped backlog issues are resolved through the documented [backlog governance](../ai/backlog-governance.md) process, either implemented or explicitly closed with reason. |
| `report_archive` | Reporting | MUST | **Met** — the GitHub issue tracker is a public, searchable archive of reports and responses. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/issues>. Only needed a URL entered in the questionnaire field. |
| `vulnerability_report_process` | Reporting | MUST | **Met** — published in [SECURITY.md](../../SECURITY.md) ("Reporting a vulnerability"). Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/SECURITY.md>. Only needed a URL entered in the questionnaire field. |
| `vulnerability_report_private` | Reporting | MUST (if applicable) | **Met** — GitHub Private Vulnerability Reporting is enabled on the repository (`gh api repos/.../private-vulnerability-reporting` → `{"enabled": true}`) and documented in SECURITY.md with the direct link. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/security/advisories/new>. Only needed a URL entered in the questionnaire field. |
| `static_analysis` | Analysis | MUST | **Met** — CodeQL (`security-extended` query suite) and SonarCloud both run in CI on every pull request and push to `main`, and are enforced as required branch-protection rules (`code_scanning` at medium-or-higher severity, `code_quality` at warnings severity) before a change can merge — i.e. before any release. Evidence: `.github/workflows/codeql.yml`, `.github/workflows/ci.yml` (`coverage_sonar` job), `docs/internal/main-ruleset-required-status-checks.json`. |
| `dynamic_analysis_enable_assertions` | Analysis | SUGGESTED | **Met** — the automated test suite (`tests/*`, NUnit) runs with assertions enabled by construction; NUnit's `Assert.*`/`Assert.That` API is the test mechanism itself, not an optional flag. |
| `delivery_unsigned` | Security | MUST | **Met** — package delivery is over HTTPS (NuGet.org, GitHub Releases/API) and release artifacts carry GitHub build provenance attestations that are cryptographically verified (`gh attestation verify`) before any hash is trusted, not a bare hash fetched over an unauthenticated channel. Evidence: [Release provenance verification guide](../guides/release-provenance-verification.md), `.github/workflows/release-nuget.yml`. |
| `vulnerabilities_critical_fixed` | Security | SHOULD | **N/A** — no critical vulnerability has ever been reported against the project (`security-advisories` API returns `[]`), so there is nothing to demonstrate timely fixing of yet. Will be revisited if one is ever reported. |

## Repository changes made for this assessment

- Added [CONTRIBUTING.md](../../CONTRIBUTING.md) to resolve the one genuine
  repository gap (`contribution_requirements`).
- No other repository, CI, or policy changes were required — every other
  previously-unmet criterion was already satisfied by existing repository
  state (SECURITY.md, branch protection, CI static analysis, release
  provenance) and only needed the corresponding questionnaire
  answer/evidence URL entered on bestpractices.dev.

## Follow-up issues

None created. Every criterion above resolved to Met/N/A using current
repository evidence or the new CONTRIBUTING.md; no genuine repository gap
remains that would justify a separate tracked issue. If a future live
submission surfaces a criterion this assessment misjudged, file a
narrowly-scoped follow-up against that specific criterion rather than
reopening #287.

## Manual step (must be done by the maintainer, authenticated on bestpractices.dev)

This document does not submit the questionnaire. A maintainer must sign in
to <https://www.bestpractices.dev/en/projects/13572> (or `/passing`) through
GitHub, enter the answers and evidence URLs above for each previously-unmet
criterion, and save/submit the self-assessment. Badge level changes only
after that manual submission is saved on the external site — no repository
change can complete it.

This is explicitly non-blocking for v0.7 product publication (see #287);
completing the manual step is independent of the release train.
