# Issue #1138 line-churn evidence

This evidence reuses the #502 History workload catalog. Inputs are identified by the catalog's `inputSha256`; expected canonical churn is checked before each sample. The checked-in [baseline data](baseline.json) records all six workload results and seven warmed direct-call samples per workload.

## Baseline identity and reproduction

- Calculator source revision: `190da651d7c12f5bc3bc2a34239a5c79698ef68d`.
- Calculator file blob: `27d5bfeabe7267541143975ed5fe2dde35316b7b`.
- The calculator file is byte-identical to the 2026-10-31 audit baseline `d74b4422c21707b1afea5e79fbdd5952597b671f`.
- Environment: .NET 10.0.14, macOS 15.8.1, x64, 6 processors, Release.
- Git used by the test-created repositories: 2.33.0.
- Each direct median is over seven warmed calls; raw elapsed-time and allocation samples are stored in JSON. Allocations use `GC.GetAllocatedBytesForCurrentThread`.

`CaptureBaseline` verifies the calculator's Git blob is the audited baseline `27d5bfeabe7267541143975ed5fe2dde35316b7b` before any measurement, and refuses to overwrite an existing `baseline.json`. Reproduce it by adding the benchmark harness files from the feature branch to a clean worktree at the baseline revision:

```sh
BASELINE_WORKTREE=/tmp/arch-linter-net-1138-baseline
git worktree add --detach "$BASELINE_WORKTREE" 190da651d7c12f5bc3bc2a34239a5c79698ef68d
git show feature/1138-adaptive-exact-line-churn:tests/ArchLinterNet.Core.Tests/History/HistoryLineChurnBenchmarkHarness.cs > "$BASELINE_WORKTREE/tests/ArchLinterNet.Core.Tests/History/HistoryLineChurnBenchmarkHarness.cs"
git show feature/1138-adaptive-exact-line-churn:tests/ArchLinterNet.Core.Tests/History/HistoryLineChurnBenchmarkModels.cs > "$BASELINE_WORKTREE/tests/ArchLinterNet.Core.Tests/History/HistoryLineChurnBenchmarkModels.cs"
(cd "$BASELINE_WORKTREE" && GIT_CONFIG_GLOBAL=/dev/null dotnet test tests/ArchLinterNet.Core.Tests -c Release --filter 'FullyQualifiedName~HistoryLineChurnBenchmarkHarness.CaptureBaseline' --logger 'console;verbosity=normal' -- NUnit.ExplicitTests=true)
git worktree remove --force "$BASELINE_WORKTREE"
```

To capture the current branch after the baseline file is present, run:

```sh
GIT_CONFIG_GLOBAL=/dev/null dotnet test tests/ArchLinterNet.Core.Tests --no-restore -c Release --filter 'FullyQualifiedName~HistoryLineChurnBenchmarkHarness.CaptureCurrent' --logger 'console;verbosity=normal' -- NUnit.ExplicitTests=true
```

`GIT_CONFIG_GLOBAL=/dev/null` isolates test-created Git repositories from a host Git signing-format setting unsupported by the installed Git 2.33.0 binary. The benchmark has no dependency changes and uses existing restore assets. Timing and allocation values are informational; deterministic output and operation-count assertions determine test pass/fail. Compare elapsed times only when baseline and current captures use the same host and runtime.

## Pre-change results

`DP cells` is the exact number of inner-loop updates in the current two-row implementation after its existing common-prefix/suffix reduction. It is the deterministic work baseline; this calculator has no internal work counter. All six actual additions/deletions matched the #502 expected-output catalog.

| Workload | Lines | Replacements | Reduced old/new | DP cells | Additions / deletions | Median ms (range) | Allocated bytes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Sparse small | 1,000 | 4 | 601 / 601 | 361,201 | 4 / 4 | 1.031 (0.981–2.391) | 257,640 |
| Sparse medium | 5,000 | 8 | 3,890 / 3,890 | 15,132,100 | 8 / 8 | 33.753 (31.393–40.833) | 1,500,976 |
| Sparse large | 10,000 | 16 | 8,824 / 8,824 | 77,862,976 | 16 / 16 | 229.428 (211.996–282.311) | 3,679,040 |
| Dense small | 1,000 | 500 | 998 / 998 | 996,004 | 500 / 500 | 2.770 (2.663–4.733) | 379,248 |
| Dense medium | 5,000 | 2,500 | 4,998 / 4,998 | 24,980,004 | 2,500 / 2,500 | 67.301 (54.718–192.540) | 1,933,976 |
| Dense large | 10,000 | 5,000 | 9,998 / 9,998 | 99,960,004 | 5,000 / 5,000 | 225.611 (201.544–336.003) | 3,906,752 |

## Pre-change ingestion phase share

The harness also ingested each 10k input as one Git file modification and checked the resulting canonical additions/deletions. The share below compares direct calculator median with the median recorded `ingestion` phase; `1/(1-p)` is only an optimistic Amdahl upper bound if all direct calculator work disappeared. It is not a product speedup estimate.

| Workload | Direct median ms | Ingestion phase median ms | Direct / phase | Amdahl upper bound |
|---|---:|---:|---:|---:|
| Sparse large | 229.428 | 210.965 | Not measured (direct sample exceeded phase median) | Not measured |
| Dense large | 225.611 | 295.446 | 76.36% | 4.231x |

The 10k sparse deterministic-work target is at most 5% of 77,862,976 baseline DP cells. Timing and allocation comparisons are informational and are not NUnit pass/fail checks. The `1/(1-p)` column is a mathematical ceiling for eliminating all calculator time, not a forecast of the measured implementation; sample variation can make its phase share unstable. Peak temporary/working-set memory was not measured; the existing fallback's two-row storage is `2 × (min(reduced old lines, reduced new lines) + 1) × sizeof(int)`.

## After measurements

The same six catalog inputs were rerun from the current branch on this machine. Input SHA-256, expected-output SHA-256, canonical additions/deletions, and output digest matched the baseline for every case. Direct timings and allocations are medians of seven warmed calls. The table shows the capture stored in [current data](current.json); those values are informative, not test gates. Raw elapsed-time and allocation samples are included in both JSON files so the median and spread can be inspected.

| Workload | Frontier steps + line comparisons | Fallback DP cells | Current / baseline median ms (range) | Median ratio | Current allocated bytes (% baseline) |
|---|---:|---:|---:|---:|---:|
| Sparse small | 41 + 634 | 0 | 0.347 (0.234–0.371) / 1.031 (0.981–2.391) | 2.98x faster | 253,072 (98.2%) |
| Sparse medium | 145 + 4,023 | 0 | 0.982 (0.872–1.693) / 33.753 (31.393–40.833) | 34.4x faster | 1,470,088 (97.9%) |
| Sparse large | 545 + 9,349 | 0 | 1.851 (1.734–2.932) / 229.428 (211.996–282.311) | 124.0x faster | 3,608,680 (98.1%) |
| Dense small | 561 + 577 | 996,004 | 5.080 (2.775–15.322) / 2.770 (2.663–4.733) | 1.83x slower by median | 379,544 (100.1%) |
| Dense medium | 561 + 577 | 24,980,004 | 93.865 (61.239–172.093) / 67.301 (54.718–192.540) | 1.40x slower by median | 1,934,272 (100.0%) |
| Dense large | 561 + 577 | 99,960,004 | 221.158 (215.556–284.849) / 225.611 (201.544–336.003) | 0.98x | 3,907,048 (100.0%) |

The 10k sparse case performs 9,894 bounded-search steps/comparisons, 0.0127% of its 77,862,976 baseline DP cells. Sparse direct sample ranges do not overlap. Dense ranges overlap substantially, including the 10k case, so the dense median differences are not reliable regression or improvement estimates. Direct timings are environmental observations and are not asserted in tests.

## Full History ingestion call

The additional comparison wrapped the entire `HistoryIngestionService.Ingest` call for one 10k-file Git modification, including Git/object processing, line churn, file evidence construction, and scoring. It used seven calls per workload on the same .NET 10.0.14 / macOS 15.8.1 / x64 / 6-processor host; raw sample distributions are in the JSON files.

| Workload | Main median (range) | Current median (range) | Median comparison |
|---|---:|---:|---:|
| Sparse 10k / 16 edits | 211.206 ms (206.400–363.030) | 3.907 ms (2.956–5.880) | 54.1x faster |
| Dense 10k / 5,000 edits | 295.749 ms (262.093–404.868) | 255.996 ms (250.013–337.222) | 13.4% lower median |

The dense sample ranges overlap substantially, so its lower median does not establish a reliable improvement. The sparse call is faster in all seven samples. This is the Core History ingestion service, not the full CLI path through policy loading and report rendering.

The bounded frontier has 67 integers (268 bytes). For the 10k dense case, the exact two-row fallback has 79,992 bytes of row storage; peak temporary/working-set memory was not measured. Dense fallback remains quadratic in time. The full-call measurements include scoring; their range and phase samples are preserved in `baseline.json` and `current.json`.
