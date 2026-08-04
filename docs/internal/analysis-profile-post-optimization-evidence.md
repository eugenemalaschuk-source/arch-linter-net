# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from final runtime commit `903061c6b019127da921c903284cd05441e5d11e`
(file version `0.1.0.0`, assembly SHA-256
`d04c6114b9233ec624d1f44c185ea3c39a62a86ff174578eccb12c65887881b0`) on
macOS 15.7.7 x64, .NET 10.0.10, six logical processors. `Analysis-only`
excludes preflight and output; `Command total` includes all measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 518 / 1060 | 6025.5 / 7595 | 6505.5 / 8230.9 | 13,371,092 / 13,388,488 |
| Cache first population | 784 / 964 | 5324.5 / 6246 | 5977.0 / 7005.7 | 137,526,392 / 137,560,040 |
| Verified warm cache hit | 619.5 / 723 | 4924 / 5282 | 5308.1 / 5664.8 | 66,949,168 / 66,954,352 |
| Sequential (`--max-parallelism 1`) | 437 / 514 | 4684 / 4926 | 5058.1 / 5340.2 | 13,371,744 / 13,401,800 |
| Default bounded parallelism | 442.5 / 456 | 4577 / 4817 | 4959.4 / 5193.3 | 13,911,952 / 13,927,064 |
| Separate strict + audit processes | 822 / 851 | 9295 / 9452 | 10048.0 / 10200.8 | 26,423,256 / 26,440,856 |
| Combined strict + audit session | 417.5 / 443 | 4603.5 / 4859 | 4997.4 / 5262.2 | 13,760,104 / 13,777,424 |
| One report sink | 441 / 446 | 4607 / 4797 | 4981.6 / 5166.5 | 13,548,168 / 13,580,248 |
| Human + JSON + SARIF sinks | 437.5 / 442 | 4532 / 4793 | 4905.8 / 5166.6 | 13,660,652 / 13,677,320 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 518 | +174 |
| Separate strict + audit | 687.5 | 822 | +134.5 |
| Combined strict + audit | 329.5 | 417.5 | +88 |
| One report sink | 343.5 | 441 | +97.5 |
| Three report sinks | 348 | 437.5 | +89.5 |

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
