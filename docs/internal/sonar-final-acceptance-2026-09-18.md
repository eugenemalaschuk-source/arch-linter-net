# Final Sonar acceptance — #797 (parent #783)

## Decision: PASS

The final `main` analysis has Quality Gate **OK**, **0 open findings**, 0 vulnerabilities, 0 bugs, 0 code smells and 0
security hotspots. 460 baseline findings went to 0 open. 22 findings were closed by an explicit reviewed disposition
(not by code); each carries a rationale comment in SonarCloud and is listed below. No project-level Sonar exclusion,
rule disablement or Quality Gate change was made to reach this result (see "Non-masking checks").

An earlier capture on `322f142f` (analysis `ca605e9b-…`) returned **FAIL** (Gate ERROR, 63 open, all untriaged). It was
superseded by PR #970 and the reviews below; that history is kept in this file's git log.

## Candidate identity

| Item | Value |
| --- | --- |
| Project / branch | `eugenemalaschuk-source_arch-linter-net` / `main` |
| Final `main` SHA | `b405b043c5324c07f28a094ff0014ae04d247406` (PR #970 squash) |
| Sonar analysis | `08c06ad8-2288-4ef5-8044-da23188292a2` (2026-09-18T18:48:13+0000), latest completed default-branch analysis, revision equals the SHA |
| Baseline (#795) | `103b2680511b702288a95ee4a16189bcec099027`, analysis `86bce771-6bc9-4856-98b7-2b2f47aa4065` |
| Capture tool | `python3 tools/release/sonar_debt_inventory.py --revision b405b043…` (revision-bound; refuses a stale analysis) |

Same project, branch, tool and Overall Code scope in both captures. Issue state is live at capture time; code identity is exact.

## Before → after

| Metric | Baseline | Final |
| --- | ---: | ---: |
| Open findings | 460 | 0 |
| Code smells | 455 | 0 |
| Vulnerabilities | 4 | 0 |
| Bugs | 1 | 0 |
| Security hotspots | 0 | 0 |
| Remediation effort (`sqale_index`, min) | 2233 | 0 |
| Security rating | 5 | 1 |
| Cognitive complexity | 14579 | 14509 |
| Duplicated lines density (%) | 0.5 | 0.5 |
| Coverage (%) | 88.1 | 88.4 |
| Quality Gate | ERROR | OK |

## How the 460 baseline findings were resolved

| Route | Evidence |
| --- | --- |
| Code remediation | PRs #877, #897, #898, #899, #901, #902, #912 (mandatory issues #887–#893) and PR #970 (residual mechanical findings, behaviour-preserving) |
| Reviewed false positive (2) | `pythonsecurity:S2083` `tools/release/main_quality_coverage.py`, `pythonsecurity:S8707` `tools/release/verify_restored_main_packages.py`; both paths pass `_release_workspace._safe_path` (#795 evidence, maintainer-confirmed) |
| Reviewed accepted, public-API compatibility (16) | S107 on public ctors/methods (`AnalysisCacheOutcomeMapper`, `AnalysisCacheOutcomeV1`, `SarifEvidenceSourceDiagnostic`, `ValidationOutcome` x2), S2325/CA1822 on public `SarifExternalDiagnosticSelector.Select`, `ArchitectureDiagnosticFormatter.FormatWaiversForHumans`, `ArchitectureSarifFormatter.FormatResultAsSarif` x3, S2365 x2, S3871, CA1068 (internal ctor, order documented in code). Changing them breaks the reviewed public API snapshot. |
| Reviewed accepted, other (3) | CA2101 x2 (`RepositoryLocalRegularFileReader`, explicit UTF-8 `[MarshalAs]`, libc ABI) and S3011 (`ArchitectureContractSurfaceExposureMemberScanner`, intentional reflection) |
| Reviewed accepted, test-only (1) | `python:S4790` `tools/badge_promotion/tests/test_setup_registry.py` — SHA-1 reproduces Git blob identity; production code already sets `usedforsecurity=False` |

Owner of every retained item: maintainer. The 16 public-API items are the input to any future breaking-change
review (next minor/major); they are not an unowned "legacy debt" bucket. A future API-breaking release may reopen them.

## Non-masking checks

- No new `sonar.exclusions`; the existing `**/AdoptionAcceptance/Fixtures/**` is unchanged since the baseline.
- `sonar.coverage.exclusions` gained `tests/qodana/**` after the baseline (test directory, coverage only, not issue scope).
  It was not introduced by this cleanup.
- Since the baseline, source additions of suppression are one rule-targeted `# NOSONAR(S4721,S8707)` on a `git show`
  subprocess call and one `[SuppressMessage]` on `FileIdentityComparer`; neither comes from this work and neither
  affects the finding count above.
- Rules, quality profile and Quality Gate conditions are unchanged. The Gate's New Code conditions are not used as evidence;
  the claim rests on the Overall Code inventory (0 open) and the key-level reconciliation.
- Sonar statuses of the 22 dispositions were set during this task on the maintainer's explicit instruction. An accepted
  status can be reverted in SonarCloud in one click.

## Integrated behaviour, architecture and API evidence (same SHA)

- Main workflows on `b405b043`: Main NuGet Builds, Main Quality Telemetry, OpenSSF Scorecard, Publish Architecture Health
  Badge — all success.
- PR #970 head CI (cross-platform unit, E2E, packed-artifact shards on Windows and macOS, Repository Lint, Architecture
  Coverage) was green before the squash; the squash tree is identical to the tested head.
- Local before merge: `make lint-architecture`, `make public-api-check`, `make lint-dotnet-format` pass; Cli tests 913 passed.
  Public API snapshots are unchanged by #970. A local full `make acceptance` was not run; CI is the authority.
- Intentional behaviour changes: none. Refactors preserve check order and identities (helpers extracted, no reordering).

## Acceptance criteria

| Criterion | Status |
| --- | --- |
| #795 and mandatory remediation issues closed | Met (#795, #887–#893) |
| Final analysis successful and bound to the final SHA | Met |
| Baseline and final comparable | Met |
| Critical findings and hotspots handled | Met (0 vulnerabilities, 0 hotspots; 2 false positives + 1 test-only reviewed) |
| Improvement not masked | Met (see non-masking checks) |
| Residual findings have owners and reviewed rationale | Met (22, maintainer, rationale in SonarCloud) |
| Architecture, API and behaviour evidence | Met (CI on the same SHA plus local gates) |
| Quality Gate green | Met (OK) |

## Follow-ups (not blockers)

- Publication of the maintenance patch and milestone closure stay with #806.
- If the 16 public-API compatibility items should be removed rather than retained, plan them in an API-breaking release.
