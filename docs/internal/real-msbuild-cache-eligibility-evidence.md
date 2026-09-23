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
| small | 7.29% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| medium | 7.42% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| large | 7.71% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |

## Cache measurements

| Fixture | Size | Mode | Projects / calibration pair | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Allocated bytes | Peak working set | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4088104 | 56057856 | 0 | 500.555 | 88.000 | 17.580% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4169808 | 56188928 | 0 | 513.421 | 85.000 | 16.556% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56168448 | 0 | 498.673 | 82.000 | 16.444% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 60905592 | 85143552 | 0 | 1837.672 | 134.000 | 7.292% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77392760 | 85217280 | 0 | 1693.559 | 112.000 | 6.613% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77382352 | 85159936 | 0 | 1818.816 | 105.000 | 5.773% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4888616 | 56778752 | 0 | 513.270 | 98.000 | 19.093% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 57040896 | 0 | 515.374 | 87.000 | 16.881% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 57143296 | 0 | 529.504 | 86.000 | 16.242% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 64111328 | 85168128 | 0 | 2439.757 | 181.000 | 7.419% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80678248 | 85143552 | 0 | 2175.891 | 120.000 | 5.515% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80675240 | 85315584 | 0 | 1877.422 | 107.000 | 5.699% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 5706968 | 57692160 | 0 | 507.737 | 91.000 | 17.923% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5797536 | 57925632 | 0 | 506.209 | 84.000 | 16.594% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5789592 | 57884672 | 0 | 510.210 | 84.000 | 16.464% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 67231768 | 85450752 | 0 | 2516.261 | 194.000 | 7.710% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83845328 | 85393408 | 0 | 2021.589 | 117.000 | 5.788% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83843352 | 85663744 | 0 | 2035.934 | 120.000 | 5.894% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

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
