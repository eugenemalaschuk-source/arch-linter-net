# SonarCloud post-architecture baseline triage

This internal report completes the audit/planning scope of [#795](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/795). It does not close the parent [#783](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/783) or claim that the current Quality Gate is green. The machine-readable matrix, frozen post-#877 key evidence, and their coverage test are [sonar-debt-triage-2026-09-14.json](sonar-debt-triage-2026-09-14.json), [sonar-debt-after-877-keys-2026-09-14.json](sonar-debt-after-877-keys-2026-09-14.json), and `tools/release/tests/test_sonar_debt_triage.py`.

OpenSpec: not applicable. This change records internal audit evidence and remediation ownership only; the Sonar inventory capability is already specified by the existing `sonarcloud-debt-inventory` capability.

## Authoritative baseline

| Field | Value |
| --- | --- |
| Sonar project / organization | `eugenemalaschuk-source_arch-linter-net` / `eugenemalaschuk-source` |
| Branch | `main` |
| Post-architecture revision | `103b2680511b702288a95ee4a16189bcec099027` |
| Completed analysis | [`86bce771-6bc9-4856-98b7-2b2f47aa4065`](https://sonarcloud.io/project/analysis?id=eugenemalaschuk-source_arch-linter-net&analysis=86bce771-6bc9-4856-98b7-2b2f47aa4065), 2026-09-13 17:14:50 UTC |
| Evidence | [JSON baseline](sonar-debt-baseline-2026-09-13.json) / [Markdown baseline](sonar-debt-baseline-2026-09-13.md) |
| Quality Gate | **ERROR** — `new_reliability_rating` 3 and `new_security_rating` 5 against threshold 1 |
| Overall findings | 460 open findings; 4 vulnerabilities; 455 code smells; 1 bug; 0 security hotspots |
| Overall measures | coverage 88.1%; duplication 0.5%; reliability rating 3.0; security rating 5.0; estimated debt 2,233 minutes |

The baseline was captured after #772 and #784/#805 completed and before the remediation slice in [#877](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/877). Its full pagination and quality-gate conditions are preserved in the JSON artifact; no quality profile, gate, exclusion, or finding status was changed to obtain it.

## Comparison capture

The Sonar inventory tool was rerun against the merged #877 tree at revision `5c83219a4bc3d1952cee22cf630c5d136ec2d71f` (analysis `646eff89-b3a0-4092-8d99-0089a04a18c9`, 2026-09-14 11:07:48 UTC; captured 21:16:42.082662 +02:00). It reports 281 persistent findings and 0 hotspots. This is a comparison capture, not a replacement baseline: Sonar issue/hotspot state is live at capture time, while the baseline's code identity is fixed to its analysis revision.

| State | Findings | Hotspots | Gate | Interpretation |
| --- | ---: | ---: | --- | --- |
| Baseline `103b268…` | 460 | 0 | ERROR | Authoritative post-architecture debt universe |
| After #877 `5c83219…` | 281 | 0 | ERROR | Frozen key evidence records 281 persistent, 179 remediated, and 0 new keys |

The frozen [key evidence](sonar-debt-after-877-keys-2026-09-14.json) stores the complete persistent/remediated/new key sets and capture timestamp. The test computes the exact set difference against the immutable baseline, proving that the 281 persistent keys and 179 absent baseline keys are the reported comparison. Its gate is still red solely because the two guarded path-injection findings remain visible to SonarCloud; they are individually reviewed below and remain protected by `_release_workspace._safe_path`.

## Complete disposition matrix

Every one of the 460 baseline findings is assigned exactly once by the JSON selectors and the coverage test. A cluster is an exact rule/path/key set, not a broad directory promise.

| Cluster | Baseline | Persistent | Disposition / owner | Exact boundary |
| --- | ---: | ---: | --- | --- |
| `remediated-by-877` | 179 | 0 | Resolved by merged [#877](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/877) | All baseline keys absent from the after capture: tests/benchmarks, Relay mechanical rules, Python mechanical rules, and Core `S2583` |
| `reviewed-false-positive-security` | 2 | 2 | Individually reviewed, retained | `pythonsecurity:S2083` at `tools/release/main_quality_coverage.py:122`; `pythonsecurity:S8707` at `tools/release/verify_restored_main_packages.py:23`; both guarded by `_safe_path` |
| `core-complexity` | 36 | 36 | Remediation [#887](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/887) | Core `csharpsquid:S3776` (26) and `S107` (10) |
| `api-risk` | 47 | 47 | Review/remediation [#888](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/888) | Core/CLI `external_roslyn:CA1859` (41 source findings), Core `S3871` (1), `S2365` (2), `S3218` (3) |
| `core-mechanical` | 94 | 94 | Remediation [#889](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/889) | Remaining Core C# rules; excludes the complexity/API-risk/S2583 sets |
| `cli-complexity` | 11 | 11 | Remediation [#890](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/890) | CLI `csharpsquid:S3776` |
| `cli-mechanical` | 54 | 54 | Remediation [#891](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/891) | Remaining CLI C# rules; excludes `S3776` and `CA1859` |
| `python-tooling` | 27 | 27 | Review/remediation [#892](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/892) | `python:S5778` (14), `S5655` (9), `S3776` (2), `S6353` (2) |
| `relay-complexity` | 10 | 10 | Remediation [#893](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/893) | Relay `typescript:S3776` |
| **Total** | **460** | **281** | **0 unassigned; 0 anonymous legacy debt** | **279 remediation-required + 2 reviewed false positives** |

The two security findings are not declared safe merely because they remain visible to SonarCloud: source review identifies the concrete sanitizer and the current gate remains honestly red. If a future Sonar analysis removes either finding, #797 must retain the review evidence and explain the change.

## Ownership and execution order

The child issues each describe one coherent change set, source/test boundary, expected evidence, and non-goals. They all depend on #795 and are required inputs to final [#797](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/797).

```text
Wave 1:  #891 CLI mechanical   +   #892 Python tooling   +   #893 Relay complexity
              |
Wave 2:  #889 Core mechanical  +   #890 CLI complexity
              |
Wave 3:  #887 Core complexity / parameter count
              |
Wave 4:  #888 Core/CLI API-risk review
              |
Final:   #797 integrated before -> after Sonar, behavior, architecture and API acceptance
```

Wave 1 has disjoint writable paths and may run as three independent lanes. The Core production lanes are serial because their findings overlap files and signatures; the CLI lanes are serial for the same reason. #888 is last because an API-risk decision can touch a signature examined by the complexity/mechanical lanes. No pair is called independent merely from its rule name.

## Validation and limits

- `python3 -m pytest tools/release/tests/test_sonar_debt_inventory.py tools/release/tests/test_sonar_debt_triage.py -q` verifies inventory behavior, direct baseline metadata/gate parity, frozen key set difference, and exact 460-row coverage.
- `python3 tools/release/sonar_debt_inventory.py --revision 5c83219a4bc3d1952cee22cf630c5d136ec2d71f --format markdown` reproduced the comparison capture and full pagination.
- Baseline source, analysis identity, quality-gate conditions, frozen key sets, and after-capture metadata are recorded without secrets or private adopter data.
- This report does not claim a green gate, final debt reduction, or #783 closure. Those claims belong to #797 after all mandatory child issues and reviews are complete.

The parent checklist must keep the chain `#795 -> #887/#888/#889/#890/#891/#892/#893 -> #797`; creating these owners does not itself resolve their findings.
