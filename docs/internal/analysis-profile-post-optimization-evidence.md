# analysis-profile/v1 post-optimization release evidence

## Scope and identity

This is the final evidence for issue #409 and PR #428. It uses the #374
`large-multi-host` fixture, real CLI subprocesses, ten successful samples per
timed scenario, and median/p95 statistics. The complete samples, profiles,
deterministic counters, and paired strict/audit profiles are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual `PostOptimizationAnalysisProfileBenchmarkHarness` ran the **Debug**
CLI built from final runtime commit `391cb1f47da92ab7bb873696e45afae3640675cb`
(file version `0.1.0.0`, assembly SHA-256
`8c9e24df5eb22ce0c0336c19c4c407f21ff2e6aa56d5e935f742fe052d38cf7c`) on
macOS 15.7.7 x64, .NET 10.0.10, six logical processors. `Analysis-only`
excludes preflight and output; `Command total` includes all measured work.

## Post-optimization results

| Scenario | Analysis median / p95 (ms) | Command median / p95 (ms) | Wall-clock median / p95 (ms) | Allocation median / p95 (bytes) |
|---|---:|---:|---:|---:|
| Warm strict, cache disabled | 437 / 548 | 4707.5 / 5102 | 5088.8 / 5482.9 | 13,370,544 / 13,388,176 |
| Cache first population | 777.5 / 906 | 5445.5 / 5916 | 6148.8 / 6802.7 | 137,489,484 / 137,494,648 |
| Verified warm cache hit | 766 / 1323 | 6472.5 / 9058 | 6940.4 / 9693.4 | 66,943,692 / 66,950,992 |
| Sequential (`--max-parallelism 1`) | 683.5 / 827 | 8683 / 11352 | 9343.4 / 12079.6 | 13,368,964 / 13,383,600 |
| Default bounded parallelism | 651 / 768 | 8578 / 9784 | 9204.6 / 10376.6 | 13,574,328 / 13,911,288 |
| Separate strict + audit processes | 1228 / 1429 | 15190.5 / 16057 | 16369.1 / 17220.0 | 26,421,692 / 26,469,456 |
| Combined strict + audit session | 620 / 747 | 7438 / 10593 | 8093.7 / 11148.6 | 13,759,328 / 13,771,128 |
| One report sink | 732.5 / 806 | 9650 / 10876 | 10238.0 / 11615.3 | 13,547,832 / 13,558,376 |
| Human + JSON + SARIF sinks | 681.5 / 754 | 9676 / 10134 | 10346.2 / 10877.2 | 13,659,416 / 13,685,352 |

## #374 baseline comparison

| Comparable scenario | #374 baseline median (ms) | Post median (ms) | Delta (ms) |
|---|---:|---:|---:|
| Immediate warm strict | 344 | 437 | +93 |
| Separate strict + audit | 687.5 | 1228 | +540.5 |
| Combined strict + audit | 329.5 | 620 | +290.5 |
| One report sink | 343.5 | 732.5 | +389 |
| Three report sinks | 348 | 681.5 | +333.5 |

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
