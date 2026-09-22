# Real-MSBuild analysis-cache eligibility evidence (#675)

## Decision

Phase 1 outcome: **C**

The current ordinary real-MSBuild matrix remains CacheIneligible with zero verified exact-request hits. The separately labelled eligibility-control attempt does not produce a verified hit, so the measured targeted-phase share is only a conservative upper bound; the normalized #991 consumer gate is still open, and available evidence does not justify an eligibility expansion or a Phase 2 implementation.

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

Targeted phase: **contract-evaluation** — Contract execution and mode-specific fact/evaluation work that a verified exact-request hit can reconstruct without re-running.
Expected equivalent reuse count: **3**. Assumptions: Three equivalent requests in one workflow or across immutable reference/base revisions; cache misses remain correct fallbacks.
Success threshold: **10.0%** amortized reduction; kill criterion: **5.0%**.

| Size | Targeted phase share | Amdahl max speedup | Cold/miss overhead | Expected warm-hit reduction | Expected amortized reduction | Avoided work | Verified hit observed |
|---|---:|---:|---:|---:|---:|---:|---|
| small | 1.01% | 1.01x | 0.00% | 1.01% | 0.68% | 4 | no |
| medium | 0.87% | 1.01x | 0.00% | 0.87% | 0.58% | 6 | no |
| large | 0.75% | 1.01x | 0.00% | 0.75% | 0.50% | 8 | no |

## Cache measurements

| Fixture | Size | Mode | Projects | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |
|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| eligible-control | small | disabled | 2 | ControlUnavailable | | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 526.531 | 21.000 | 3.988% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | population | 2 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 528.117 | 25.000 | 4.734% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | small | repeat | 2 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 551.889 | 19.000 | 3.443% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | disabled | 2 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1677.483 | 17.000 | 1.013% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | population | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 0 | 1600.836 | 18.000 | 1.124% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| real-msbuild | small | repeat | 2 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 2 | 0 | 0 | 0 | 1593.227 | 17.000 | 1.067% | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| eligible-control | medium | disabled | 4 | ControlUnavailable | | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 497.145 | 18.000 | 3.621% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | population | 4 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 579.000 | 18.000 | 3.109% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | medium | repeat | 4 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 532.023 | 19.000 | 3.571% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | disabled | 4 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2070.563 | 18.000 | 0.869% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | population | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 0 | 1854.311 | 17.000 | 0.917% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| real-msbuild | medium | repeat | 4 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 4 | 0 | 0 | 0 | 1903.732 | 19.000 | 0.998% | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| eligible-control | large | disabled | 6 | ControlUnavailable | | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 497.680 | 18.000 | 3.617% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | population | 6 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 533.351 | 19.000 | 3.562% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| eligible-control | large | repeat | 6 | ControlUnavailable | | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 536.891 | 19.000 | 3.539% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | disabled | 6 | CacheIneligible | framework-reference-identity-unverified | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2385.563 | 18.000 | 0.755% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | population | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 0 | 2050.805 | 18.000 | 0.878% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |
| real-msbuild | large | repeat | 6 | CacheIneligible | framework-reference-identity-unverified | 1 | 0 | 0 | 1 | 0 | 6 | 0 | 0 | 0 | 2102.495 | 30.000 | 1.427% | 9900043f60a2e5ed85ff329000971763cc6e1c3d8ffaaf26ac7a5966e20bd5ed |

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
