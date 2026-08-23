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
| `vulnerability_report_response` | **N/A (changed from Met)** | This criterion is time-windowed to the last 6 months (2026-02-23–2026-08-23 at assessment time). Zero vulnerability reports were filed against the project in that window (`security-advisories` API returns `[]`), so there is no report to measure a response time against. The questionnaire's own guidance is to answer N/A when the window is empty, not to inherit a stale `Met`; private-reporting being enabled is orthogonal to response latency and does not itself support `Met`. |

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
| `tests_are_added` | Met (unchanged, now with concrete evidence) | The [feature implementation workflow](../ai/feature-implementation-workflow.md) requires tests for behavior changes, and recent major changes show the policy actually followed: [PR #620](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/620) (release-forensics dogfooding) added/modified 14 C# test files under `tests/ArchLinterNet.Core.Tests/History/`; [PR #627](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/627) (bind versioned release scopes) added substantial coverage to 3 Python test files (e.g. `+278/-92` in `test_create_release_scope_evidence.py`). |
| `tests_documented_added` | Met (unchanged, barely-met per July record) | Same evidence as `test_policy`; documentation exists but is process-level rather than a dedicated testing-policy page. |
| `warnings` | Met (unchanged) | `Directory.Build.props` enables compiler warnings solution-wide. |
| `warnings_fixed` | Met (unchanged) | `TreatWarningsAsErrors` — a warning fails the build, so warnings cannot accumulate unaddressed. |
| `warnings_strict` | **Unmet (changed from Met)** | `TreatWarningsAsErrors` only escalates whatever warnings are already enabled to build failures; it does not by itself maximize *which* warnings are enabled. `Directory.Build.props` sets `Nullable=enable` and relies on the SDK's default `AnalysisLevel`/`WarningLevel`, but does not set `<AnalysisMode>All</AnalysisMode>` or an equivalent `<AnalysisLevel>latest-all</AnalysisLevel>` — the practical, low-cost knob for enabling the full built-in Roslyn analyzer rule set, which the SDK does not turn on by default. Since a practical stricter setting exists and is not enabled, this SUGGESTED criterion is recorded as genuinely `Unmet` rather than stretched from `TreatWarningsAsErrors` alone; it does not block Metal `passing`. |

## Security (16 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `know_secure_design` | Met (unchanged) | Self-attested; primary developer familiarity with secure-design principles, unchanged since July. |
| `know_common_errors` | Met (unchanged) | Self-attested; CodeQL's `security-extended` query suite and SonarCloud rule set operationalize this knowledge in CI. |
| `crypto_published` | Met (unchanged, evidence corrected) | The project's own security-relevant crypto mechanism — [`AnalysisCacheContentDigest`](../../src/ArchLinterNet.Core/Caching/AnalysisCacheContentDigest.cs) authenticating analysis-cache entries — uses only standard, publicly reviewed primitives (HMAC-SHA256, .NET `RandomNumberGenerator`), not a project-authored algorithm. The release pipeline additionally uses SHA-256 digests and GitHub's Sigstore-based build attestation. |
| `crypto_call` | Met (unchanged, evidence corrected) | `AnalysisCacheContentDigest.Compute` calls `HMACSHA256.HashData` and `AnalysisCacheHmacKeyStore.GetOrCreateKey` calls `RandomNumberGenerator.GetBytes` — both standard .NET BCL APIs, never hand-rolled cryptography. |
| `crypto_floss` | Met (unchanged, evidence corrected) | The .NET cryptography stack (`System.Security.Cryptography`, used directly by the analysis-cache HMAC mechanism) and GitHub's attestation tooling are both FLOSS. |
| `crypto_keylength` | **Met (unchanged, evidence corrected — was previously missing the real product mechanism)** | `AnalysisCacheHmacKeyStore` generates a 32-byte (256-bit) HMAC key via `RandomNumberGenerator.GetBytes(KeyLengthBytes)` (`KeyLengthBytes = 32`), used with HMAC-SHA256 — well above adequate key-length guidance for a MAC key. This is not just the default: `TryReadExistingKey` (line 136) accepts a persisted key only when `bytes.Length == KeyLengthBytes` exactly, treating any shorter (or longer) key file as absent and triggering regeneration — a smaller key can never be loaded or used, not merely discouraged. The prior evidence cited only the release pipeline's SHA-256/Sigstore key material and missed this mechanism entirely. |
| `crypto_working` | **Met (unchanged, evidence corrected)** | The analysis-cache authentication mechanism uses HMAC-SHA256 exclusively, with `CryptographicOperations.FixedTimeEquals` for constant-time tag comparison (`AnalysisCacheContentDigest.Verify`) — no broken algorithm (e.g. MD5/SHA-1) is used as a default anywhere in that path or the release/verification pipeline. |
| `crypto_weaknesses` | **Met (unchanged, evidence corrected)** | Same mechanism as `crypto_working`: HMAC-SHA256 with a 256-bit random key and constant-time verification avoids known weak primitives and the timing side channel a naive string comparison would introduce. |
| `crypto_pfs` | **N/A (changed from Met)** | This criterion concerns key-agreement protocols implemented by the software itself. ArchLinterNet implements no such protocol; GitHub/NuGet.org TLS termination is third-party delivery infrastructure (already the evidence for `delivery_mitm`), not a mechanism the project produces. Not applicable, not merely satisfied by someone else's transport. |
| `crypto_password_storage` | **N/A (changed from Met)** | This criterion applies to software that enforces inbound password authentication. ArchLinterNet has no such feature — "stores no user passwords" is evidence of non-applicability, not of a met password-hashing requirement. |
| `crypto_random` | **Met (unchanged, evidence corrected)** | `AnalysisCacheHmacKeyStore.CreateExclusiveOrAdoptWinner` generates its HMAC key with `RandomNumberGenerator.GetBytes` (a CSPRNG), the project's actual security-sensitive random-value generation; no project code uses a non-cryptographic RNG (e.g. `System.Random`) for a security-relevant value. |
| `delivery_mitm` | Met (unchanged) | All distribution channels (NuGet.org, GitHub Releases, GitHub Pages) are HTTPS-only. |
| `delivery_unsigned` | **Met (changed from Unmet — was genuinely stale)** | The July `Unmet` answer predates #611/#612/#627 (merged in August 2026), which added GitHub build provenance attestation for every release `.nupkg`/`.snupkg` and the canonical manifest/checksum files, verified with `gh attestation verify` before any digest is trusted. A hash is never fetched over an unauthenticated channel and used without a signature check. Evidence: [Release provenance verification guide](../guides/release-provenance-verification.md), `.github/workflows/release-nuget.yml`. This is the clearest example in this re-audit of a July answer that was accurate then and stale now. |
| `vulnerabilities_fixed_60_days` | Met (unchanged, evidence corrected — was previously repo-scoped only) | `security-advisories` API returning `[]` only proves no GitHub Security Advisory exists *for this repository*; it says nothing about public CVE/OSV/NVD records against ArchLinterNet's own published packages. Checked both, dated 2026-08-23: (1) `POST https://api.osv.dev/v1/query` for `ArchLinterNet.Cli` and `ArchLinterNet.Core` (NuGet ecosystem) each return `{}` — no known vulnerability record for either published package. (2) `gh api repos/.../dependabot/alerts` lists 6 dependency alerts to date, all `state: fixed`, all against third-party dependencies (`System.Security.Cryptography.Xml`, `pymdown-extensions`), none against project-authored code; the slowest was fixed in 13 days and the fastest in 37 minutes — comfortably inside the 60-day window. |
| `vulnerabilities_critical_fixed` | **Met (changed from Unknown)** | No critical vulnerability has ever been reported against the project (`security-advisories` API returns `[]`), so no report exists that was *not* fixed rapidly. Re-assess the first time a critical vulnerability is actually reported; SECURITY.md documents a coordination process but no fixed response-time SLA, so this is not a forward-looking promise. Note: this criterion's form only accepts `Met`/`Unmet`/`?` (no `N/A`); `Met` with this no-reports justification is the defensible value, not `N/A`. |
| `no_leaked_credentials` | Met (unchanged) | GitHub secret scanning and push protection are both enabled (`gh api repos/.../  --jq '.security_and_analysis'`); no credential leak is known. |

## Analysis (8 criteria)

| Criterion ID | Status | Evidence / re-audit note |
|---|---|---|
| `static_analysis` | **Met (changed from Unknown)** | CodeQL (`security-extended` query suite) runs on every pull request, every push to `main`, and weekly, and is a required branch-protection check (`code_scanning`, medium-or-higher severity) — this alone satisfies the MUST criterion. SonarCloud additionally runs via the `coverage_sonar` job (required check `Coverage + Sonar`) on `pull_request` events, but only for same-repository, non-Dependabot PRs (`TRUSTED_PR` gate in `.github/workflows/ci.yml`); it does not run as a merge gate on direct pushes to `main`, and its analysis steps are skipped (the check still passes trivially) for fork or Dependabot PRs, which lack the `SONAR_TOKEN` secret. Do not cite SonarCloud as covering "every PR and push" — CodeQL is the evidence that carries this MUST criterion; SonarCloud is corroborating but narrower than that. Evidence: `.github/workflows/codeql.yml`, `.github/workflows/ci.yml` (`coverage_sonar` job and `TRUSTED_PR` condition), `docs/internal/main-ruleset-required-status-checks.json`. |
| `static_analysis_common_vulnerabilities` | Met (unchanged) | CodeQL's `security-extended` suite includes common vulnerability-class queries. |
| `static_analysis_fixed` | Met (unchanged) | Branch protection's `code_scanning` rule blocks merge on medium-or-higher CodeQL alerts, so findings are fixed before merge by construction. |
| `static_analysis_often` | Met (unchanged) | CodeQL runs on every PR, every push to `main`, and on a weekly schedule (`cron: "17 4 * * 1"`). |
| `dynamic_analysis` | Met (unchanged, now with coverage evidence) | An automated test suite only counts as this criterion's dynamic-analysis tool when it exercises at least 80% branch coverage. Downloaded the four `dotnet-coverage-*` Cobertura shard reports from the CI run for this PR's head commit and merged them with `reportgenerator` (`MultiReport`, 4 Cobertura inputs — naively summing the raw shard XML overstates or understates the true figure because each shard's report spans the whole solution, not just its own slice): merged branch coverage is **81.6% (15,069 of 18,449)** as of 2026-08-23, above the 80% bar. This is point-in-time evidence, not a standing gate: branch protection's `code_coverage` rule enforces `minimum_coverage: 80` on **line** coverage (confirmed via `main-ruleset-required-status-checks.json`), which is a different, separately-tracked metric from branch coverage — it does not itself keep branch coverage above 80% on every future change. Re-measure branch coverage the same way before relying on this criterion for a future major release, or add a dedicated branch-coverage gate if a standing guarantee is wanted. |
| `dynamic_analysis_unsafe` | **N/A (changed from Met)** | The questionnaire directs projects that do not produce software in a memory-unsafe language to answer N/A, not Met. `grep -rn '\bunsafe\b' src/` finding no `unsafe` C# blocks is exactly the evidence for that non-applicability (managed .NET is memory-safe by construction) — it does not turn the criterion into something the project actively satisfies. |
| `dynamic_analysis_enable_assertions` | **Unmet (changed from Unknown)** | This criterion asks for runtime assertions (e.g. `Debug.Assert`/`Contract.Assert`) inside the software being analyzed, enabled during dynamic analysis — not test-oracle assertions in the test framework itself. `grep -rn 'Debug\.Assert\|Contract\.Assert\|Trace\.Assert' src/` finds zero matches: no production code carries invariant assertions today. NUnit's `Assert.*`/`Assert.That` express expected test outcomes and do not satisfy this criterion. Recorded as genuinely `Unmet`; this is SUGGESTED-level and does not block passing. |
| `dynamic_analysis_fixed` | Met (unchanged) | No known unfixed defect surfaced by the test suite; test failures block merge via required CI checks. |

## Repository changes made for this assessment

- Added [CONTRIBUTING.md](../../CONTRIBUTING.md) to resolve the one genuine
  repository gap found (`contribution_requirements`).
- No other repository, CI, or policy changes were required — every other
  previously-unmet or previously-stale criterion was already satisfied by
  existing repository state (SECURITY.md, enabled GitHub private
  vulnerability reporting, branch-protection-enforced CodeQL, the
  analysis-cache's HMAC-SHA256/`RandomNumberGenerator` mechanism, the
  #611/#612/#627 release-provenance work) and only needed the
  corresponding questionnaire answer/evidence recorded. This does not
  include `dynamic_analysis_enable_assertions` or `warnings_strict` — see
  "Follow-up issues" below for why those two stay genuinely `Unmet`.

## Follow-up issues

None created. 65 of the 67 criteria above resolve to Met or N/A using
current repository evidence or the new CONTRIBUTING.md. Two exceptions are
recorded as genuinely `Unmet`, both SUGGESTED-level so neither blocks Metal
`passing`:

- `dynamic_analysis_enable_assertions` — no runtime assertions exist in
  production source (test-oracle assertions in NUnit do not count).
- `warnings_strict` — `TreatWarningsAsErrors` escalates existing warnings
  to build failures but does not maximize which warnings are enabled; the
  SDK's full analyzer rule set (`<AnalysisMode>All</AnalysisMode>` or
  equivalent) is not turned on.

Neither is treated as a repository gap requiring a follow-up issue, per the
issue's own "create only real follow-ups" guidance — enabling the full
analyzer set or adding invariant assertions purely to satisfy these
SUGGESTED criteria would be questionnaire-driven, not requirement-driven,
and `<AnalysisMode>All</AnalysisMode>` in particular is known to surface a
large first-time diagnostic backlog that is out of scope for this
documentation-only task. If a future live submission surfaces a criterion
this re-audit misjudged, file a narrowly-scoped follow-up against that
specific criterion rather than reopening #287.

## Manual step (must be done by the maintainer, authenticated on bestpractices.dev)

This document does not submit the questionnaire. A maintainer must sign in
to <https://www.bestpractices.dev/en/projects/13572> (or `/passing`) through
GitHub, enter the answers and evidence above for every criterion whose
status changed in this re-audit, and save/submit the self-assessment. Badge
level changes only after that manual submission is saved on the external
site — no repository change can complete it.

This is explicitly non-blocking for v0.7 product publication (see #287);
completing the manual step is independent of the release train.
