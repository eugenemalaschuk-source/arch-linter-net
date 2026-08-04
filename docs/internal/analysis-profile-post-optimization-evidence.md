# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from source commit `c0740b6` (file version `0.1.0.0`, assembly SHA-256
`e2320d774477ddb010938ef42797f7c515c4113d1c5bb8d1d42e746466378e2b`) on
macOS 15.7.7 x64, .NET 10.0.10, six logical processors. `Analysis-only`
excludes preflight and output; `Command total` includes all measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 430.5 / 445 | 4570.5 / 4666 | 4975.5 / 5071.1 | 13,370,588 / 13,372,304 |
| Cache first population | 596 / 625 | 4712.5 / 4912 | 5363.6 / 5550.5 | 86,086,328 / 86,092,568 |
| Verified warm cache hit | 529 / 573 | 4672.5 / 4853 | 5086.8 / 5282.2 | 32,678,616 / 32,689,856 |
| Sequential (`--max-parallelism 1`) | 433.5 / 443 | 4555 / 4895 | 4967.0 / 5302.0 | 13,371,468 / 13,391,456 |
| Default bounded parallelism | 433 / 440 | 4609 / 4705 | 5015.4 / 5091.6 | 13,910,724 / 13,943,000 |
| Separate strict + audit processes | 806 / 830 | 9172 / 9295 | 10000.1 / 10103.7 | 13,369,656 / 13,371,432 |
| Combined strict + audit session | 410 / 459 | 4606.5 / 4730 | 5032.6 / 5181.4 | 13,758,292 / 13,771,920 |
| One report sink | 431.5 / 437 | 4597 / 4945 | 5010.5 / 5354.3 | 13,548,492 / 13,566,200 |
| Human + JSON + SARIF sinks | 430.5 / 437 | 4565.5 / 4706 | 4974.4 / 5115.7 | 13,659,040 / 13,678,216 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 430.5 | +86.5 |
| Separate strict + audit | 687.5 | 806 | +118.5 |
| Combined strict + audit | 329.5 | 410 | +80.5 |
| One report sink | 343.5 | 431.5 | +88 |
| Three report sinks | 348 | 430.5 | +82.5 |

The #374 corpus did not measure comparable verified-cache or bounded-parallel
counters; those scenarios are therefore reported as new evidence, not invented
baseline deltas.

## Correctness gates

- Every warm-hit sample reports `Hits = 1`, `AssemblyLoads = 0`,
  `AvoidedAssemblyLoads = 20`, positive avoided fact/contract work, and no
  `contract_checks` phase. Each canonical result equals its uncached baseline.
- Every parallel sample reports `Status = Active`, `MaxParallelism = 4`,
  `ScheduledWorkItems = CompletedWorkItems = 30`,
  `ObservedMaxConcurrency = 4`, `MergeOperations = 3`, and
  `FactIndexMaterializations = 1`. Each canonical result equals its paired
  sequential sample.
- The raw paired strict/audit evidence preserves both profiles independently.
  All equivalence checks include ordered findings, completion status, exit code,
  publication state, and deterministic cache/parallel counters.

This is one declared Debug-environment measurement, not a universal performance
guarantee. Deterministic counters and equivalence gates are the normative
correctness evidence.
