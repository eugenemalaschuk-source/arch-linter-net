# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from final runtime commit `e1da749cff29bb921478d78d703d2ad9d9f8fdb3`
(file version `0.1.0.0`, assembly SHA-256
`358f69192eb01990bbb68f7c4a881a5051972085269b1e04e3cd804f1a9d0248`) on
macOS 15.7.7 x64, .NET 10.0.10, six logical processors. `Analysis-only`
excludes preflight and output; `Command total` includes all measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 428.5 / 433 | 4533 / 4669 | 4892.4 / 5028.1 | 13,370,848 / 13,375,872 |
| Cache first population | 733 / 864 | 4813 / 4976 | 5414.7 / 5577.5 | 137,530,428 / 137,539,552 |
| Verified warm cache hit | 605.5 / 623 | 4725 / 4846 | 5081.3 / 5209.4 | 66,949,236 / 67,010,624 |
| Sequential (`--max-parallelism 1`) | 435.5 / 478 | 4475 / 4660 | 4836.6 / 5018.8 | 13,370,848 / 13,387,176 |
| Default bounded parallelism | 434.5 / 449 | 4468.5 / 4566 | 4825.4 / 4926.8 | 13,912,808 / 13,928,912 |
| Separate strict + audit processes | 812 / 837 | 8940.5 / 9099 | 9657.7 / 9854.6 | 26,423,388 / 26,446,920 |
| Combined strict + audit session | 412 / 420 | 4471.5 / 4701 | 4849.8 / 5085.0 | 13,759,892 / 13,777,832 |
| One report sink | 428.5 / 465 | 4478.5 / 4885 | 4840.9 / 5261.1 | 13,548,060 / 13,559,888 |
| Human + JSON + SARIF sinks | 433 / 461 | 4529 / 4677 | 4889.9 / 5148.9 | 13,659,364 / 13,679,776 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 428.5 | +84.5 |
| Separate strict + audit | 687.5 | 812 | +124.5 |
| Combined strict + audit | 329.5 | 412 | +82.5 |
| One report sink | 343.5 | 428.5 | +85 |
| Three report sinks | 348 | 433 | +85 |

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
