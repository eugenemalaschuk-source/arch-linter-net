# OpenSSF Best Practices — Metal-series `passing` assessment

Tracks: #287. Related: #286 (badge display, closed), #257 (trust badge story).

Badge series: **Metal-series `passing`** (the classic Best Practices
passing/silver/gold questionnaire), project `13572`. This is explicitly
**not** the separate OpenSSF Baseline series (`baseline-1/2/3`), which is a
different questionnaire on the same site and is out of scope for this
assessment — see the issue's "Target badge series" note.

Project record: <https://www.bestpractices.dev/en/projects/13572>

Criteria/version observed: the passing-level questionnaire as rendered by
bestpractices.dev on the assessment date below, last saved by the project
record on **2026-07-11 12:08:22 UTC** per the site's own timestamp.

Assessment date: 2026-08-23. Live status observed at that date: **84%**
against the passing-level criteria, 67 criteria total. The site's own
automated project-page percentage may drift as later answers are submitted;
this document is point-in-time re-audit evidence, not a live mirror.

## How to use this document

This is repository-safe preparation evidence for the maintainer's manual
questionnaire submission on bestpractices.dev (see "Manual step" below). It
is not itself the questionnaire. Answers below are conservative: a
criterion is marked evidence-backed only where current repository state
genuinely satisfies it, using the project's actual solo-maintainer,
AI-assisted, early-preview operating model — no invented community size,
SLA, or support commitment.

Every one of the 67 passing-level criteria is re-audited against current
repository state below, not only the criteria that were unmet at the start
of this task. The July 2026 record predates several relevant repository
changes (most notably the #611/#612/#627 release-provenance work), so a
`Met` answer inherited unchanged from July is called out explicitly where
this re-audit found no reason to revisit it, and any answer that changed is
called out with why.

## Basics (13 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `description_good` | Met (unchanged) | README opens with a one-line description of what the tool does. |
| `interact` | Met (unchanged) | README, SECURITY.md, and now CONTRIBUTING.md describe how to interact with the project. |
| `contribution` | Met (unchanged) | Contribution process documented via [Pull Request Governance](../ai/pull-request-governance.md) and [Backlog governance](../ai/backlog-governance.md), now also summarized in CONTRIBUTING.md. |
| `contribution_requirements` | **Met (changed from Unmet)** | Added [CONTRIBUTING.md](../../CONTRIBUTING.md) in this task: coding conventions, formatting, warnings-as-errors, tests, and architecture-governance requirements for contributions. |
| `floss_license` | Met (unchanged) | [LICENSE](../../LICENSE) is MIT. |
| `floss_license_osi` | Met (unchanged) | MIT is an OSI-approved license. |
| `license_location` | Met (unchanged) | `LICENSE` at repository root, linked from README. |
| `documentation_basics` | Met (unchanged) | Public docs site: <https://eugenemalaschuk-source.github.io/arch-linter-net/>. |
| `documentation_interface` | Met (unchanged) | CLI/policy interface documented under the "Usage" and "Policy Authoring" nav sections in `mkdocs.yml`. |
| `sites_https` | Met (unchanged) | GitHub, GitHub Pages, and NuGet.org all serve HTTPS only. |
| `discussion` | Met (unchanged) | GitHub Issues is a searchable discussion mechanism. |
| `english` | Met (unchanged) | All documentation is in English. |
| `maintained` | Met (unchanged) | Active commit/release history; latest release `v0.6.5` (2026-08-15), multiple releases in the two weeks before this assessment. |

## Change Control (9 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `repo_public` | Met (unchanged) | Public GitHub repository. |
| `repo_track` | Met (unchanged) | Git tracks change author/date for every commit. |
| `repo_interim` | Met (unchanged) | Commit history contains many commits between tagged releases, not release-only snapshots. |
| `repo_distributed` | Met (unchanged) | Git is a distributed VCS. |
| `version_unique` | Met (unchanged) | Each release has a unique tag (`v0.6.1`–`v0.6.5` observed). |
| `version_semver` | Met (unchanged) | Versioning follows SemVer (`MAJOR.MINOR.PATCH`, `0.x` preview). |
| `version_tags` | Met (unchanged) | Releases are tagged in git; confirmed via `gh release list`. |
| `release_notes` | Met (unchanged) | Every GitHub Release carries categorized, human-readable notes (Breaking Changes / Features / Fixes / Documentation / CI-CD / Dependencies / Other Changes) generated from PR titles per `.github/release.yml`; not raw version-control logs. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/releases>. |
| `release_notes_vulns` | N/A (unchanged; N/A is an accepted value for this criterion) | No publicly known runtime vulnerability with a CVE (or similar) has been fixed in any release, so there is nothing to identify. Confirmed: `gh api repos/eugenemalaschuk-source/arch-linter-net/security-advisories` returns `[]`. |

## Reporting (8 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `report_process` | Met (unchanged) | Bug report process documented in SECURITY.md and via the GitHub issue tracker. |
| `report_tracker` | Met (unchanged) | GitHub Issues is used for tracking. |
| `report_responses` | **Met (changed from Unmet)** | The project is solo-maintainer and early-preview; every issue to date is self-filed by the maintainer (`gh issue list --state all` shows a single distinct author across 300 issues) and is triaged/closed via a linked pull request (e.g. #491, #486). There are no external bug reports in the 2–12 month window, so none are outstanding without a response. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/issues?q=is%3Aissue+label%3Abug>. |
| `enhancement_responses` | **Met (changed from Unmet)** | Same evidence pattern as `report_responses`: enhancement-shaped backlog issues are resolved through the documented [backlog governance](../ai/backlog-governance.md) process, either implemented or explicitly closed with reason. |
| `report_archive` | **Met (changed from Unmet)** | The GitHub issue tracker is a public, searchable archive of reports and responses. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/issues>. This criterion only needed a URL entered in the questionnaire field; no repository gap. |
| `vulnerability_report_process` | **Met (changed from Unmet)** | Published in [SECURITY.md](../../SECURITY.md) ("Reporting a vulnerability"). Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/SECURITY.md>. Only needed a URL entered in the questionnaire field. |
| `vulnerability_report_private` | **Met (changed from Unmet)** | GitHub Private Vulnerability Reporting is enabled on the repository (`gh api repos/eugenemalaschuk-source/arch-linter-net/private-vulnerability-reporting` → `{"enabled": true}`) and documented in SECURITY.md with the direct link. Evidence: <https://github.com/eugenemalaschuk-source/arch-linter-net/security/advisories/new>. Only needed a URL entered in the questionnaire field. |
| `vulnerability_report_response` | Met (unchanged) | Inherited from July; no vulnerability report has been filed since to reassess against. Re-audit found no reason to revisit: private reporting stays enabled and SECURITY.md's coordination process is unchanged. |

## Quality (13 criteria — all inherited unchanged, re-confirmed)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `build` | Met (unchanged) | `dotnet build ArchLinterNet.slnx` / `make restore` + `make build`. |
| `build_common_tools` | Met (unchanged) | Standard .NET SDK / MSBuild tooling. |
| `build_floss_tools` | Met (unchanged) | The .NET SDK is FLOSS. |
| `test` | Met (unchanged) | NUnit test suite under `tests/`. |
| `test_invocation` | Met (unchanged) | `dotnet test tests/<Project> --no-restore` / `make test`. |
| `test_most` | Met (unchanged) | Coverage gate enforces a minimum of 80% via branch protection (`code_coverage`, `minimum_coverage: 80`); Codecov reports coverage per PR. |
| `test_continuous_integration` | Met (unchanged) | `.github/workflows/ci.yml` runs on every PR and push to `main`. |
| `test_policy` | Met (unchanged) | Testing expectations documented in the [feature implementation workflow](../ai/feature-implementation-workflow.md) validation lifecycle. |
| `tests_are_added` | Met (unchanged) | Governance workflow requires tests for behavior changes; enforced by review process, not automated gate. |
| `tests_documented_added` | Met (unchanged, barely-met per July record) | Same evidence as `test_policy`; documentation exists but is process-level rather than a dedicated testing-policy page. |
| `warnings` | Met (unchanged) | `Directory.Build.props` enables compiler warnings solution-wide. |
| `warnings_fixed` | Met (unchanged) | `TreatWarningsAsErrors` — a warning fails the build, so warnings cannot accumulate unaddressed. |
| `warnings_strict` | Met (unchanged) | Same `TreatWarningsAsErrors` setting is the strictest available mode. |

## Security (16 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `know_secure_design` | Met (unchanged) | Self-attested; primary developer familiarity with secure-design principles, unchanged since July. |
| `know_common_errors` | Met (unchanged) | Self-attested; CodeQL's `security-extended` query suite and SonarCloud rule set operationalize this knowledge in CI. |
| `crypto_published` | Met (unchanged) | Only publicly reviewed cryptography is used: SHA-256 for manifest/package digests, GitHub's Sigstore-based build attestation for signing. No project-authored cryptographic algorithm exists. |
| `crypto_call` | Met (unchanged) | Cryptographic primitives are called via standard .NET/GitHub-provided libraries, never hand-rolled. |
| `crypto_floss` | Met (unchanged) | The .NET cryptography stack and GitHub's attestation tooling are FLOSS. |
| `crypto_keylength` | Met (unchanged) | SHA-256 and GitHub Sigstore-issued key material meet current adequate-length guidance; no project-chosen short keys exist. |
| `crypto_working` | Met (unchanged) | No broken algorithm (e.g. MD5/SHA-1) is used as a default anywhere in the release/verification pipeline. |
| `crypto_weaknesses` | Met (unchanged) | Same evidence as `crypto_working`; no known-weak primitive is used by default. |
| `crypto_pfs` | Met (unchanged) | TLS termination for GitHub/NuGet.org (the only network transports involved) supports forward secrecy; the project does not run its own TLS endpoint. |
| `crypto_password_storage` | Met (unchanged) | The project stores no user passwords; nothing to hash. |
| `crypto_random` | Met (unchanged) | No project code generates security-sensitive random values outside standard, unmodified .NET/GitHub-provided APIs. |
| `delivery_mitm` | Met (unchanged) | All distribution channels (NuGet.org, GitHub Releases, GitHub Pages) are HTTPS-only. |
| `delivery_unsigned` | **Met (changed from Unmet — was genuinely stale)** | The July `Unmet` answer predates #611/#612/#627 (merged in August 2026), which added GitHub build provenance attestation for every release `.nupkg`/`.snupkg` and the canonical manifest/checksum files, verified with `gh attestation verify` before any digest is trusted. A hash is never fetched over an unauthenticated channel and used without a signature check. Evidence: [Release provenance verification guide](../guides/release-provenance-verification.md), `.github/workflows/release-nuget.yml`. This is the clearest example in this re-audit of a July answer that was accurate then and stale now. |
| `vulnerabilities_fixed_60_days` | Met (unchanged) | No known unpatched vulnerability exists; `security-advisories` API returns `[]`. |
| `vulnerabilities_critical_fixed` | **Met (changed from Unknown)** | No critical vulnerability has ever been reported against the project (`security-advisories` API returns `[]`), so no report exists that was *not* fixed rapidly. Re-assess the first time a critical vulnerability is actually reported; SECURITY.md documents a coordination process but no fixed response-time SLA, so this is not a forward-looking promise. Note: this criterion's form only accepts `Met`/`Unmet`/`?` (no `N/A`); `Met` with this no-reports justification is the defensible value, not `N/A`. |
| `no_leaked_credentials` | Met (unchanged) | GitHub secret scanning and push protection are both enabled (`gh api repos/.../  --jq '.security_and_analysis'`); no credential leak is known. |

## Analysis (8 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `static_analysis` | **Met (changed from Unknown)** | CodeQL (`security-extended` query suite) runs on every pull request, every push to `main`, and weekly, and is a required branch-protection check (`code_scanning`, medium-or-higher severity) — this alone satisfies the MUST criterion. SonarCloud additionally runs via the `coverage_sonar` job (required check `Coverage + Sonar`) on `pull_request` events, but only for same-repository, non-Dependabot PRs (`TRUSTED_PR` gate in `.github/workflows/ci.yml`); it does not run as a merge gate on direct pushes to `main`, and its analysis steps are skipped (the check still passes trivially) for fork or Dependabot PRs, which lack the `SONAR_TOKEN` secret. Do not cite SonarCloud as covering "every PR and push" — CodeQL is the evidence that carries this MUST criterion; SonarCloud is corroborating but narrower than that. Evidence: `.github/workflows/codeql.yml`, `.github/workflows/ci.yml` (`coverage_sonar` job and `TRUSTED_PR` condition), `docs/internal/main-ruleset-required-status-checks.json`. |
| `static_analysis_common_vulnerabilities` | Met (unchanged) | CodeQL's `security-extended` suite includes common vulnerability-class queries. |
| `static_analysis_fixed` | Met (unchanged) | Branch protection's `code_scanning` rule blocks merge on medium-or-higher CodeQL alerts, so findings are fixed before merge by construction. |
| `static_analysis_often` | Met (unchanged) | CodeQL runs on every PR, every push to `main`, and on a weekly schedule (`cron: "17 4 * * 1"`). |
| `dynamic_analysis` | Met (unchanged) | The NUnit test suite is dynamic analysis; it runs in CI on every PR. |
| `dynamic_analysis_unsafe` | Met (unchanged) | No `unsafe` C# code exists in `src/` (confirmed via `grep -rn '\bunsafe\b' src/`, only comment/string matches); the project is memory-safe by construction as fully managed .NET code. |
| `dynamic_analysis_enable_assertions` | **Unmet (changed from Unknown)** | This criterion asks for runtime assertions (e.g. `Debug.Assert`/`Contract.Assert`) inside the software being analyzed, enabled during dynamic analysis — not test-oracle assertions in the test framework itself. `grep -rn 'Debug\.Assert\|Contract\.Assert\|Trace\.Assert' src/` finds zero matches: no production code carries invariant assertions today. NUnit's `Assert.*`/`Assert.That` express expected test outcomes and do not satisfy this criterion. Recorded as genuinely `Unmet`; this is SUGGESTED-level and does not block passing. |
| `dynamic_analysis_fixed` | Met (unchanged) | No known unfixed defect surfaced by the test suite; test failures block merge via required CI checks. |

## Repository changes made for this assessment

- Added [CONTRIBUTING.md](../../CONTRIBUTING.md) to resolve the one genuine
  repository gap found (`contribution_requirements`).
- No other repository, CI, or policy changes were required — every other
  previously-unmet or previously-stale criterion was already satisfied by
  existing repository state (SECURITY.md, enabled GitHub private
  vulnerability reporting, branch-protection-enforced CodeQL, the
  #611/#612/#627 release-provenance work, GitHub-native NUnit assertions)
  and only needed the corresponding questionnaire answer/evidence recorded.

## Follow-up issues

None created. 66 of the 67 criteria above resolve to Met or N/A using
current repository evidence or the new CONTRIBUTING.md. The one exception,
`dynamic_analysis_enable_assertions`, is genuinely `Unmet` (no runtime
assertions exist in production source) and is SUGGESTED-level, so it does
not block Metal `passing`; it is not treated as a repository gap requiring
a follow-up issue, per the issue's own "create only real follow-ups"
guidance — adding invariant assertions purely to satisfy this SUGGESTED
criterion would be questionnaire-driven, not requirement-driven. If a
future live submission surfaces a criterion this re-audit misjudged, file a
narrowly-scoped follow-up against that specific criterion rather than
reopening #287.

## Manual step (must be done by the maintainer, authenticated on bestpractices.dev)

This document does not submit the questionnaire. A maintainer must sign in
to <https://www.bestpractices.dev/en/projects/13572> (or `/passing`) through
GitHub, enter the answers and evidence above for every criterion whose
status changed in this re-audit, and save/submit the self-assessment. Badge
level changes only after that manual submission is saved on the external
site — no repository change can complete it.

This is explicitly non-blocking for v0.7 product publication (see #287);
completing the manual step is independent of the release train.
