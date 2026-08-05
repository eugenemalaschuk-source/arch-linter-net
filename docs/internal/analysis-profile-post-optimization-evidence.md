# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from final runtime commit `1e78f0b2e2a5fabb9f5c17e52e05888fcc4caa69`
(file version `0.1.0.0`, executed-assembly SHA-256
`0e6c818edc73e13bfd65d8c79db489efa9d905046ca38d626f4f2af69d9dc53b`). The
same harness selected exactly one packed CLI package: `ArchLinterNet.Cli`
version `0.1.0-preview.658`, package SHA-256
`fc7f30cb1dd970d9b1cea8995aad516834d5a54696e967be62fa2c8d77ae8836`.
The measurement ran on macOS 15.7.7 x64, .NET 10.0.10, six logical processors.
`Analysis-only` excludes preflight and output; `Command total` includes all
measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 496.5 / 603 | 5326 / 5739 | 5796.9 / 6173.4 | 13,379,488 / 13,395,672 |
| Cache first population | 750.5 / 900 | 5031 / 5468 | 5649.6 / 6104.0 | 137,553,200 / 137,569,352 |
| Verified warm cache hit | 615 / 637 | 4633 / 4788 | 5000.9 / 5159.5 | 66,958,184 / 66,961,480 |
| Sequential (`--max-parallelism 1`) | 432.5 / 443 | 4462 / 4590 | 4818.7 / 4941.7 | 13,378,432 / 13,380,472 |
| Default bounded parallelism | 438.5 / 448 | 4442.5 / 4762 | 4794.1 / 5114.3 | 13,919,464 / 13,934,464 |
| Separate strict + audit processes | 813 / 823 | 8797 / 9279 | 9505.9 / 9982.9 | 26,436,820 / 26,455,472 |
| Combined strict + audit session | 413.5 / 431 | 4448 / 4539 | 4824.0 / 4925.4 | 13,766,060 / 13,783,744 |
| One report sink | 433 / 442 | 4430.5 / 4555 | 4791.1 / 4928.1 | 13,556,216 / 13,567,840 |
| Human + JSON + SARIF sinks | 435 / 446 | 4454.5 / 4569 | 4815.1 / 4925.7 | 13,675,384 / 13,680,584 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 496.5 | +152.5 |
| Separate strict + audit | 687.5 | 813 | +125.5 |
| Combined strict + audit | 329.5 | 413.5 | +84 |
| One report sink | 343.5 | 433 | +89.5 |
| Three report sinks | 348 | 435 | +87 |

The #374 corpus did not measure comparable verified-cache or bounded-parallel
counters; those scenarios are therefore reported as new evidence, not invented
baseline deltas.

## Correctness gates

- Every warm-hit sample reports `Hits = 1`, `AssemblyLoads = 0`,
  `AvoidedAssemblyLoads = 10`, `AvoidedFactIndexMaterializations = 1`,
  `AvoidedContractExecutions = 2`, and `AvoidedArtifactBytesLoaded = 155392`.
  These counters are persisted measured population work, not configuration
  heuristics; no warm sample has a `contract_checks` phase.
- Every parallel sample reports `Status = Active`, `MaxParallelism = 4`,
  `ScheduledWorkItems = CompletedWorkItems = 30`,
  `ObservedMaxConcurrency = 4`, `MergeOperations = 3`, and
  `FactIndexMaterializations = 1`. Each canonical result equals its paired
  sequential sample.
- The raw paired strict/audit evidence preserves both profiles independently.
  Its allocation is the sum of both profiles, and both initial and
  post-ensure-built preflight phases are excluded from `Analysis-only`.
  All equivalence checks include ordered findings, completion status, exit code,
  publication state, and deterministic cache/parallel counters.

This is one declared Debug-environment measurement, not a universal performance
guarantee. Deterministic counters and equivalence gates are the normative
correctness evidence.
