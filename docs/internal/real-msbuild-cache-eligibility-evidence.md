# Real-MSBuild analysis-cache eligibility evidence (#675)

## Decision

Phase 1 outcome: **B**

The expanded cache-avoidable boundary includes assembly/artifact loading and analysis work, but the separately labelled eligibility-control attempt does not produce a verified hit. The warm-hit and amortized effect therefore remain model-only and cannot support a final no-value outcome C; route the incomplete evidence through the #991 normalization and owning prepared-analysis lanes before making a final eligibility decision.

Phase 2 remains gated by #991: status **open**, authorized: **False**.
Do not begin Phase 2 until #991 completes and the normalized dogfood workflows are remeasured; any future outcome A must be recorded only after that gate.

## Methodology

- Reuse #502's synthetic workload generator/materializer; no second benchmark corpus is created.
- Real-MSBuild rows retain the observed fail-closed eligibility and typed reasons. The separately labelled staged control is reported as unavailable when its artifact authorization cannot produce a verified hit; it is never counted as a real-MSBuild success.
- Deterministic counters establish avoided-work scope; Stopwatch values are environment-labelled supporting evidence.
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

| Size | Targeted phase share | Amdahl max speedup | Cold/miss overhead | Expected warm-hit reduction | Expected amortized reduction | Avoided work | Verified hit observed |
|---|---:|---:|---:|---:|---:|---:|---|
| small | 7.06% | 1.08x | 0.00% | unavailable | unavailable | unavailable | no |
| medium | 9.21% | 1.10x | 0.00% | unavailable | unavailable | unavailable | no |
| large | 7.94% | 1.09x | 0.00% | unavailable | unavailable | unavailable | no |

## Cache measurements

| Fixture | Size | Mode | Projects | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 552.776 | 88.000 | 15.920% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 509.829 | 81.000 | 15.888% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 557.714 | 80.000 | 14.344% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2011.031 | 142.000 | 7.061% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 0 | 1710.547 | 104.000 | 6.080% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 0 | 1658.984 | 117.000 | 7.053% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 514.035 | 100.000 | 19.454% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 512.253 | 84.000 | 16.398% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 508.562 | 86.000 | 16.910% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2107.099 | 194.000 | 9.207% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 0 | 1916.262 | 122.000 | 6.367% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 0 | 1861.847 | 112.000 | 6.016% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 | ControlUnavailable |  | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 536.874 | 94.000 | 17.509% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 535.545 | 89.000 | 16.619% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 | ControlUnavailable |  | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 520.793 | 84.000 | 16.129% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2343.554 | 186.000 | 7.937% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 0 | 2070.698 | 117.000 | 5.650% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 0 | 2018.111 | 117.000 | 5.798% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

## Reference/base-side exact-request disposition

- Disposition: **routed-to-owning-lane**
- Counted in candidate exact-cache effect estimate: **False**
- Evidence: The current exact-request cache boundary covers validation modes, while immutable reference/base preparation is a separate consumer/prepared-analysis concern; it is routed separately and never counted as candidate cache savings.

## Stale-input dispositions

| Change kind | Disposition | Evidence |
|---|---|---|
| artifact | reject | Selected PE/PDB/build-receipt byte changes are covered by existing artifact-manifest rejection tests. |
| package-or-configuration | reject | Package/framework/reference/configuration/build identity changes alter authorization or cache key. |
| project | reject | Existing evaluated-manifest digest comparison rejects changed project/import inputs. |
| source | reject-or-ineligible | Source inputs outside the exact manifest remain fail-closed; no timestamp-only authorization is used. |

OpenSpec: not applicable. This artifact records internal benchmark/evidence tooling and changes no runtime, policy, cache authorization, public API, or documented user guarantee.
No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is included.
