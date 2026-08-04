# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from final runtime commit `4096709b84f111330689611919bd7e1cf9689209`
(file version `0.1.0.0`, assembly SHA-256
`0d400c2003c06e2059888762e55c6ee18f8dabff2347f57adbc05aacd576820b`) on
macOS 15.7.7 x64, .NET 10.0.10, six logical processors. `Analysis-only`
excludes preflight and output; `Command total` includes all measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 537 / 1153 | 4905.5 / 11167 | 5281.6 / 11921.1 | 13,371,456 / 13,372,424 |
| Cache first population | 744 / 785 | 5219.5 / 5475 | 5842.2 / 6137.3 | 137,516,088 / 137,534,328 |
| Verified warm cache hit | 628.5 / 654 | 5020 / 5470 | 5380.9 / 5844.7 | 66,945,324 / 66,953,952 |
| Sequential (`--max-parallelism 1`) | 442.5 / 466 | 4877.5 / 4964 | 5303.2 / 5385.0 | 13,371,276 / 13,382,752 |
| Default bounded parallelism | 436 / 520 | 4571 / 4998 | 4923.8 / 5382.5 | 13,913,940 / 13,928,072 |
| Separate strict + audit processes | 814 / 827 | 8878 / 9034 | 9604.7 / 9740.4 | 26,423,560 / 26,439,184 |
| Combined strict + audit session | 411.5 / 426 | 4453.5 / 4606 | 4833.4 / 4993.9 | 13,765,332 / 13,776,704 |
| One report sink | 437 / 441 | 4444.5 / 4518 | 4803.3 / 4884.0 | 13,548,848 / 13,553,336 |
| Human + JSON + SARIF sinks | 427.5 / 436 | 4470 / 4735 | 4832.3 / 5101.3 | 13,659,844 / 13,677,784 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 537 | +193 |
| Separate strict + audit | 687.5 | 814 | +126.5 |
| Combined strict + audit | 329.5 | 411.5 | +82 |
| One report sink | 343.5 | 437 | +93.5 |
| Three report sinks | 348 | 427.5 | +79.5 |

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
