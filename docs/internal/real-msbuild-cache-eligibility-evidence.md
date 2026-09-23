# Real-MSBuild analysis-cache eligibility evidence (#675)

## Decision

Phase 1 decision state: **Pending**

The expanded cache-avoidable boundary includes assembly/artifact loading and analysis work, but the separately labelled eligibility-control attempt does not produce a verified hit. The effect estimate is incomplete, so the Phase 1 decision remains pending and cannot support a final A, B, or C outcome; route the incomplete evidence through the #991 normalization and owning prepared-analysis lanes before making a final eligibility decision.

Phase 2 remains gated by #991: status **open**, authorized: **False**.
Do not begin Phase 2 until #991 completes and the normalized dogfood workflows are remeasured; any future outcome A must be recorded only after that gate.

## Methodology

- Reuse #502's synthetic workload generator/materializer; no second benchmark corpus is created.
- Real-MSBuild rows retain the observed fail-closed eligibility and typed reasons. The separately labelled staged control is reported as unavailable when its artifact authorization cannot produce a verified hit; it is never counted as a real-MSBuild success.
- The targeted boundary includes assembly/artifact loading and analysis phases that a verified exact-request hit skips; cache lookup, build-state authorization and output routing remain outside it. Deterministic counters establish avoided-work scope; Stopwatch values are environment-labelled supporting evidence.
- Without a verified warm-hit control, warm-hit and amortized reductions remain unavailable/model-only and cannot be used to declare a final outcome C.
- Cold/miss overhead is computed only from the eligible-control disabled-versus-population path when eligibility, miss, and cache write are all verified; its absolute cost is normalized against the real-MSBuild disabled baseline, and ineligible or rejected real-MSBuild rows are never used as amortization cost.
- Expected warm-hit reduction is normalized to the real-MSBuild denominator by applying the observed control targeted-work avoidance fraction to the real targeted-phase share; it cannot exceed the real Amdahl bound.
- Outcome-complete resource evidence requires allocation, peak working set, bytes read, and bytes written observations for eligible-control disabled, population, and repeat paths.
- Eligible-control calibration is paired to the corresponding real-MSBuild workload by project count and an explicit calibration-pair identity; mismatches leave the estimate incomplete.
- Cache-disabled, population/miss, and repeat results retain canonical-result identity within each fixture and across eligible-control versus real-MSBuild workloads; stale-input checks retain fail-closed dispositions.
- The exact-request cache estimate excludes prepared-analysis persistence and separately records reference/base-side work.

## Environment

- Runtime: .NET 10.0.12
- Operating system: Microsoft Windows 10.0.26200
- Architecture: X64
- Configuration: Debug
- Tool identity: ArchLinterNet.Cli analysis-profile/v1
- Source identity: synthetic-current-tree

## Pre-implementation effect estimate

Targeted phase: **cache-avoidable-analysis** — Assembly/artifact loading and analysis phases skipped by a verified exact-request hit; cache lookup, build-state authorization, and output routing remain outside the boundary.
Expected equivalent reuse count: **3**. Assumptions: Three equivalent requests in one workflow or across immutable reference/base revisions; cache misses remain correct fallbacks.
Success threshold: **10.0%** amortized reduction; kill criterion: **5.0%**.

| Size | Targeted phase share | Amdahl max speedup | Cold/miss overhead | Expected warm-hit reduction | Expected amortized reduction | Avoided work | Verified hit observed | Resource evidence |
|---|---:|---:|---:|---:|---:|---:|---|---|
| small | 7.00% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| medium | 7.82% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| large | 7.75% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |

## Cache measurements

| Fixture | Size | Mode | Projects / calibration pair | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Allocated bytes | Peak working set | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4079856 | 56016896 | 0 | 523.503 | 98.000 | 18.720% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56086528 | 0 | 514.566 | 84.000 | 16.324% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56156160 | 0 | 525.518 | 95.000 | 18.077% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 60908496 | 84992000 | 0 | 1927.786 | 135.000 | 7.003% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77385912 | 85299200 | 0 | 1672.482 | 106.000 | 6.338% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77372136 | 85221376 | 0 | 1679.572 | 110.000 | 6.549% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4880416 | 56799232 | 0 | 515.871 | 92.000 | 17.834% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 56864768 | 0 | 527.311 | 87.000 | 16.499% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 56913920 | 0 | 522.829 | 89.000 | 17.023% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 64103744 | 85204992 | 0 | 2135.591 | 167.000 | 7.820% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80655736 | 85413888 | 0 | 1897.881 | 114.000 | 6.007% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80674864 | 85413888 | 0 | 1875.578 | 116.000 | 6.185% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 5708504 | 57610240 | 0 | 526.778 | 98.000 | 18.604% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5797536 | 57765888 | 0 | 546.739 | 91.000 | 16.644% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5797536 | 57933824 | 0 | 556.243 | 90.000 | 16.180% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 67211712 | 85409792 | 0 | 2438.854 | 189.000 | 7.750% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83844688 | 85487616 | 0 | 2100.668 | 120.000 | 5.712% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83845528 | 85385216 | 0 | 2166.816 | 123.000 | 5.677% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

## Reference/base-side exact-request disposition

- Disposition: **routed-to-owning-lane**
- Counted in candidate exact-cache effect estimate: **False**
- Evidence: The current exact-request cache boundary covers validation modes, while immutable reference/base preparation is a separate consumer/prepared-analysis concern; it is routed separately and never counted as candidate cache savings.

## Stale-input dispositions

| Change kind | Disposition | Evidence |
|---|---|---|
| artifact | reject | Selected PE/PDB/build-receipt byte changes are covered by existing artifact-manifest rejection tests. |
| configuration | reject | Configuration/TFM/platform/RID and build identity changes reject reuse or alter the cache key. |
| package | reject | Package/framework/reference identity changes reject reuse or alter the cache key. |
| project | reject | Existing evaluated-manifest digest comparison rejects changed project/import inputs. |
| source | reject | Source inputs outside the exact manifest reject reuse; no timestamp-only authorization is used. |

OpenSpec: not applicable. This artifact records internal benchmark/evidence tooling and changes no runtime, policy, cache authorization, public API, or documented user guarantee.
No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is included.
