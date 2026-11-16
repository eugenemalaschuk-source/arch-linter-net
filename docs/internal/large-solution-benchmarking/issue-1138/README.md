# Issue #1138 line-churn evidence

This evidence reuses the #502 History workload catalog. Inputs are identified by the catalog's `inputSha256`; expected canonical churn is checked before each sample. The checked-in [baseline data](baseline.json) records all six workload results and five warmed direct-call samples per workload.

## Baseline identity and reproduction

- Calculator source revision: `190da651d7c12f5bc3bc2a34239a5c79698ef68d`.
- Calculator file blob: `27d5bfeabe7267541143975ed5fe2dde35316b7b`.
- The calculator file is byte-identical to the 2026-10-31 audit baseline `d74b4422c21707b1afea5e79fbdd5952597b671f`.
- Environment: .NET 10.0.14, macOS 15.8.1, x64, 6 processors, Release.
- Git used by the test-created repositories: 2.33.0.
- Each direct median is over five warmed calls; allocations use `GC.GetAllocatedBytesForCurrentThread`.

Run from the repository root:

```sh
GIT_CONFIG_GLOBAL=/dev/null dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter 'FullyQualifiedName~HistoryLineChurnBenchmarkHarness.CaptureBaseline' --logger 'console;verbosity=normal' -- NUnit.ExplicitTests=true
```

`GIT_CONFIG_GLOBAL=/dev/null` isolates test-created Git repositories from a host Git signing-format setting unsupported by the installed Git 2.33.0 binary. The benchmark has no dependency changes and uses existing restore assets.

## Pre-change results

`DP cells` is the exact number of inner-loop updates in the current two-row implementation after its existing common-prefix/suffix reduction. It is the deterministic work baseline; this calculator has no internal work counter. All six actual additions/deletions matched the #502 expected-output catalog.

| Workload | Lines | Replacements | Reduced old/new | DP cells | Additions / deletions | Median ms | Allocated bytes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Sparse small | 1,000 | 4 | 601 / 601 | 361,201 | 4 / 4 | 1.157 | 257,640 |
| Sparse medium | 5,000 | 8 | 3,890 / 3,890 | 15,132,100 | 8 / 8 | 38.700 | 1,500,976 |
| Sparse large | 10,000 | 16 | 8,824 / 8,824 | 77,862,976 | 16 / 16 | 166.207 | 3,679,040 |
| Dense small | 1,000 | 500 | 998 / 998 | 996,004 | 500 / 500 | 2.730 | 379,248 |
| Dense medium | 5,000 | 2,500 | 4,998 / 4,998 | 24,980,004 | 2,500 / 2,500 | 55.861 | 1,933,976 |
| Dense large | 10,000 | 5,000 | 9,998 / 9,998 | 99,960,004 | 5,000 / 5,000 | 228.812 | 3,906,752 |

## Ingestion phase share

The harness also ingested each 10k input as one Git file modification and checked the resulting canonical additions/deletions. The share below compares direct calculator median with the median recorded `ingestion` phase; `1/(1-p)` is only an optimistic Amdahl upper bound if all direct calculator work disappeared. It is not a product speedup estimate.

| Workload | Direct median ms | Ingestion phase median ms | Direct / phase | Amdahl upper bound |
|---|---:|---:|---:|---:|
| Sparse large | 154.066 | 199.035 | 77.41% | 4.426x |
| Dense large | 197.059 | 262.567 | 75.05% | 4.008x |

The 10k sparse values establish the issue-specific target: deterministic fast-path work must be at most 5% of 77,862,976 baseline DP cells and the direct median must improve by at least 2x. The 10k dense fallback must stay within 1.25x of 197.059 ms and within 105% of 3,906,752 allocated bytes. Peak temporary/working-set memory was not measured; the existing fallback's two-row storage is `2 × (min(reduced old lines, reduced new lines) + 1) × sizeof(int)`.

## After measurements

The same six catalog inputs were rerun twice on this machine. Input SHA-256, expected-output SHA-256, canonical additions/deletions, and output digest matched the baseline for every case. Direct timings are medians of five warmed calls; allocated bytes are per-call medians. The table shows the latest capture, stored in [current data](current.json).

| Workload | Frontier steps + line comparisons | Fallback DP cells | Current / baseline median ms | Speedup / regression | Current allocated bytes (% baseline) |
|---|---:|---:|---:|---:|---:|
| Sparse small | 41 + 634 | 0 | 0.244 / 1.157 | 4.75x faster | 253,072 (98.2%) |
| Sparse medium | 145 + 4,023 | 0 | 0.901 / 38.700 | 42.96x faster | 1,470,088 (97.9%) |
| Sparse large | 545 + 9,349 | 0 | 2.417 / 166.207 | 68.75x faster | 3,608,680 (98.1%) |
| Dense small | 561 + 577 | 996,004 | 4.044 / 2.730 | 1.481x | 379,544 (100.1%) |
| Dense medium | 561 + 577 | 24,980,004 | 66.637 / 55.861 | 1.193x | 1,934,272 (100.0%) |
| Dense large | 561 + 577 | 99,960,004 | 245.110 / 228.812 | 1.071x | 3,907,048 (100.0%) |

The 10k sparse case performs 9,894 bounded-search steps/comparisons, 0.0127% of its 77,862,976 baseline DP cells, and exceeds the 2x timing target. In the latest paired capture, the 10k dense case is within the 1.25x timing gate (1.071x) and the 105% allocation gate. An earlier set of current-only captures, compared with the original 197.059 ms dense baseline, measured 219.801, 243.032, and 293.339 ms (1.115x, 1.233x, and 1.489x). A fresh baseline capture of the unchanged original calculator then measured 228.812 ms, followed by 245.110 ms for the committed implementation (1.071x); this confirms substantial host timing drift. The paired capture is the reported comparison. The 1k dense case also varies at small absolute times and is shown for context, not as an acceptance gate.

The bounded frontier has 67 integers (268 bytes). For the 10k dense case, the exact two-row fallback has 79,992 bytes of row storage; peak temporary/working-set memory was not measured. Dense fallback remains quadratic in time. In the latest paired Git ingestion capture, the direct calculator was 37.13% of the 10k sparse ingestion phase (6.509 ms) and 96.67% of the 10k dense phase (253.563 ms); corresponding optimistic Amdahl upper bounds are 1.591x and 29.997x. These phase shares are observations, not product speedup claims.
