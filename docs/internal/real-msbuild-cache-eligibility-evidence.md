# Real-MSBuild analysis-cache eligibility evidence (#675)

## Decision

Phase 1 outcome: **B**

The expanded cache-avoidable boundary includes assembly/artifact loading and analysis work, but the separately labelled eligibility-control attempt does not produce a verified hit. The warm-hit and amortized effect therefore remain model-only and cannot support a final no-value outcome C; route the incomplete evidence through the #991 normalization and owning prepared-analysis lanes before making a final eligibility decision.

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
- Cache-disabled, population/miss, and repeat results retain canonical-result identity; stale-input checks retain fail-closed dispositions.
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
| small | 6.02% | 1.06x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| medium | 7.46% | 1.08x | unavailable | unavailable | unavailable | unavailable | no | unavailable |
| large | 8.00% | 1.09x | unavailable | unavailable | unavailable | unavailable | no | unavailable |

## Cache measurements

| Fixture | Size | Mode | Projects | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Allocated bytes | Peak working set | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4079904 | 56066048 | 0 | 547.234 | 95.000 | 17.360% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4194152 | 56127488 | 0 | 525.714 | 85.000 | 16.168% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4185952 | 56156160 | 0 | 518.642 | 86.000 | 16.582% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 60908632 | 85262336 | 0 | 2442.979 | 147.000 | 6.017% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77370680 | 85147648 | 0 | 1685.780 | 105.000 | 6.229% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 77384896 | 85155840 | 0 | 1662.735 | 115.000 | 6.916% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4880416 | 56844288 | 0 | 562.601 | 107.000 | 19.019% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 56987648 | 0 | 553.532 | 88.000 | 15.898% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 4994320 | 56905728 | 0 | 549.421 | 93.000 | 16.927% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 64100000 | 85258240 | 0 | 2199.095 | 164.000 | 7.458% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80667712 | 85106688 | 0 | 1897.101 | 116.000 | 6.115% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 80665808 | 85118976 | 0 | 1959.934 | 164.000 | 8.368% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 5706968 | 57569280 | 0 | 564.071 | 112.000 | 19.856% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5818832 | 57823232 | 0 | 558.960 | 99.000 | 17.711% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 5797536 | 57819136 | 0 | 552.991 | 90.000 | 16.275% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 67212560 | 85303296 | 0 | 2499.398 | 200.000 | 8.002% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83856808 | 85200896 | 0 | 2092.197 | 121.000 | 5.783% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 83859544 | 85245952 | 0 | 2078.615 | 121.000 | 5.821% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

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
