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
- Decision classification is total after #991: A requires every S/M/L point at or above the success threshold, C requires every point at or below the kill criterion, and B covers complete useful or mixed-scale evidence between those uniform outcomes.
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
| small | 7.86% | 1.09x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| medium | 7.33% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| large | 7.51% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |

## Cache measurements

| Fixture | Size | Mode | Projects / calibration pair | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Allocated bytes | Peak working set | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4071656 | 55906304 | 0 | 496.870 | 87.000 | 17.510% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56156160 | 0 | 557.966 | 87.000 | 15.592% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56246272 | 0 | 554.915 | 89.000 | 16.038% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 60913072 | 85372928 | 0 | 1845.086 | 145.000 | 7.859% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77396032 | 85393408 | 0 | 1658.476 | 110.000 | 6.633% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 / synthetic-cache-eligibility-small-p2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77388984 | 85086208 | 0 | 1691.234 | 119.000 | 7.036% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4880416 | 56803328 | 0 | 513.268 | 89.000 | 17.340% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4977920 | 57147392 | 0 | 518.736 | 84.000 | 16.193% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 57090048 | 0 | 540.596 | 88.000 | 16.278% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 64142112 | 85311488 | 0 | 2127.334 | 156.000 | 7.333% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80670704 | 85274624 | 0 | 1887.228 | 116.000 | 6.147% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 / synthetic-cache-eligibility-medium-p4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80678200 | 85184512 | 0 | 1965.874 | 109.000 | 5.545% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 5697000 | 57720832 | 0 | 557.346 | 97.000 | 17.404% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5805736 | 57876480 | 0 | 501.149 | 81.000 | 16.163% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5797536 | 57946112 | 0 | 542.679 | 93.000 | 17.137% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 67207232 | 85508096 | 0 | 2462.727 | 185.000 | 7.512% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83865272 | 85372928 | 0 | 2084.323 | 124.000 | 5.949% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 / synthetic-cache-eligibility-large-p6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83849872 | 85331968 | 0 | 2075.708 | 128.000 | 6.167% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

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
