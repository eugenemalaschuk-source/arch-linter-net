# Final Sonar acceptance — #797 (parent #783)

## Decision: FAIL (not PASS)

#783 must not be closed on this evidence. Two independent reasons, each sufficient:

1. The Quality Gate on the final analysis is **ERROR** (`new_security_rating` = 5, threshold 1).
1. **63 open findings** remain, and every one has disposition `untriaged` in the capture: no reviewed
   `accepted` / `false-positive` decision is recorded for any of them, including the two guarded
   security findings that #795 already identified as false positives.

No exclusion, suppression, rule change, or Sonar status change was made to influence this result.

## Candidate identity

| Item | Value |
| --- | --- |
| Project / branch | `eugenemalaschuk-source_arch-linter-net` / `main` |
| Final `main` SHA | `322f142f6cd4fb81312e06679e6876fe14a409f2` |
| Sonar analysis | `ca605e9b-cc05-46cb-9930-2a4327a878ca` (2026-09-18T14:42:59+0000) |
| Freshness | Latest completed default-branch analysis; revision equals the SHA above (checked by `tools/release/sonar_debt_inventory.py --revision`) |
| Baseline (#795) | `103b2680511b702288a95ee4a16189bcec099027`, analysis `86bce771-6bc9-4856-98b7-2b2f47aa4065` |
| Captured | 2026-09-18T16:28Z; issue disposition is live at capture time, code identity is exact |

The baseline and final captures use the same project, branch, tool, and Overall Code scope. The
`sonar-debt-after-877-keys` capture (281 findings) is an intermediate point and is not used as the baseline.

## Before → after

| Metric | Baseline | Final |
| --- | ---: | ---: |
| Open findings | 460 | 63 |
| Code smells | 455 | 60 |
| Vulnerabilities | 4 | 3 |
| Bugs | 1 | 0 |
| Security hotspots | 0 | 0 |
| Remediation effort (`sqale_index`, min) | 2233 | 421 |
| Cognitive complexity | 14579 | 14554 |
| Duplicated lines density (%) | 0.5 | 0.5 |
| Coverage (%) | 88.1 | 88.4 |
| Quality Gate | ERROR | ERROR |

Key-level reconciliation: 426 baseline keys resolved, 34 baseline keys persist, 29 keys are new (426 + 34 = 460; 34 + 29 = 63).
Resolution is attributable to the merged remediation PRs #877, #897, #898, #899, #901, #902 and #912, but this report did not
audit each PR key-by-key. Cognitive complexity is essentially flat, so the complexity-debt reduction is not evidenced by the aggregate.

## Findings that block acceptance

### Persistent baseline findings (34)

Owned by closed remediation issues #887, #888, #889 and #891, yet still open with no reviewed disposition:

| Rule | Count | Area |
| --- | ---: | --- |
| `csharpsquid:S107` | 5 | Core Caching / Models / Validation |
| `csharpsquid:S2325`, `S2365`, `S3011`, `S3267`, `S3358` (x3), `S3398`, `S3871`, `S3928` | 11 | Core |
| `csharpsquid:S8969` | 3 | Core RawValidators |
| `external_roslyn:CA1822` / `CA1859` / `CA1068` / `CA2101` / `CA2208` | 13 | Core Reporting/Execution/IO/Validation, CLI |
| `pythonsecurity:S2083`, `pythonsecurity:S8707` | 2 | `tools/release` — reviewed as false positives in #795 evidence (guarded by `_release_workspace._safe_path`) but still open in Sonar |

The two `pythonsecurity` findings are what keep `new_security_rating` at 5 and the Gate at ERROR.

### New keys since the baseline (29)

Not owned by any #783 child, so they are unrouted:

- C#: `S107`, `S3776` x2, `S2325`, `S3220`, `CA1822`, `CA1859` in Core Execution/Validation/BuildState/Validators; `S1118`, `S8969` x2 in Cli (`Validate/Application`, `Badge/Application/Setup`).
- Python: `python:S3358` x4, `S3776` x3, `S6353` x2, `S8786` x2, `S5778` x3, `S4790` (test file), `S5713`, `S5843`, `S6326`, `S9073` in `tools/release`, `tools/badge_promotion`, `tools/scripts`, `tests/qodana`.

They are consistent with the badge/promotion/release-tooling work merged after the baseline; this report does not claim each key's origin.
`python:S4790` is a Sonar-classified vulnerability in `tools/badge_promotion/tests/test_setup_registry.py:39` and needs its own review.

## Acceptance criteria status

| Criterion | Status |
| --- | --- |
| #795 and mandatory remediation issues closed | Met (#795, #887–#893 all closed) |
| Final analysis successful and bound to final SHA | Met |
| Baseline/final comparable | Met |
| Critical findings and hotspots handled per #783 | Not met — 3 open vulnerabilities, 2 without recorded review |
| Improvement not masked by exclusions/suppressions | No masking observed |
| Residual findings have owners and reviewed rationale | **Not met** — 63 untriaged |
| Architecture/API/behaviour evidence at this SHA | CI on `322f142f` shows Main NuGet Builds, Main Quality Telemetry, Scorecard and Badge workflows successful. A local `make acceptance` was **not** run, because the Sonar outcome already decides this task |
| Quality Gate green | **Not met** — ERROR |

## Required next steps (product/status decisions, not made here)

1. Maintainer review of the two guarded `pythonsecurity` findings and the `python:S4790` test finding; record `false-positive`/`accepted` in Sonar with justification, or fix them.
1. Route the 29 new keys and the 32 non-security persistent keys to a narrow remediation issue (or per-finding reviewed retention with an owner) under #783.
1. Re-capture on the resulting `main` SHA with `python3 tools/release/sonar_debt_inventory.py --revision <sha>` and repeat this acceptance.
