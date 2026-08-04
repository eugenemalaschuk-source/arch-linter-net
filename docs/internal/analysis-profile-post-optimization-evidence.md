# analysis-profile/v1 post-optimization release evidence

## Scope and method

This is the final post-cache/post-parallel reference evidence for issue #409.
It reuses the #374 synthetic `large-multi-host` corpus, real CLI subprocesses,
`analysis-profile/v1` phase boundaries, and the same median/p95 calculation.
The complete raw profiles and deterministic counters are checked in as
[`analysis-profile-post-optimization-results.json`](analysis-profile-post-optimization-results.json).

The manual harness is `PostOptimizationAnalysisProfileBenchmarkHarness` and is
explicitly excluded from ordinary acceptance. It used the Debug CLI artifact
with file version `0.1.0.0`, .NET 10.0.10, macOS 15.7.7 x64, and six logical
processors. `Analysis-only` excludes preflight and rendering/publication;
`Output` is rendering/publication; `Command total` retains all measured work.
Each timed row has ten successful samples. Failure runs remain raw evidence but
are excluded from timing statistics.

## Results

| Scenario | n | Analysis-only median / p95 (ms) | Output median / p95 (ms) | Command-total median / p95 (ms) | Preflight median (ms) |
|---|---:|---:|---:|---:|---:|
| Warm strict, cache disabled | 10 | 352 / 385 | 42.5 / 45 | 4569 / 4788 | 4158.5 |
| Cache first population | 10 | 396.5 / 408 | 42 / 44 | 4560.5 / 4705 | 4119 |
| Verified warm cache hit | 10 | 431 / 453 | 18 / 21 | 4583 / 4700 | 4136.5 |
| Sequential (`--max-parallelism 1`) | 10 | 353 / 364 | 41.5 / 45 | 4495 / 4544 | 4099 |
| Default bounded parallelism | 10 | 352.5 / 404 | 42 / 44 | 4494.5 / 4597 | 4099 |
| Separate strict + audit processes | 10 | 705.5 / 719 | 83 / 87 | 9089 / 9200 | 8301.5 |
| Combined strict + audit session | 10 | 332.5 / 361 | 44 / 57 | 4539.5 / 4602 | 4162 |
| One report sink | 10 | 352.5 / 367 | 42 / 45 | 4545 / 4863 | 4149 |
| Human + JSON + SARIF sinks | 10 | 352.5 / 356 | 51 / 54 | 4532 / 4588 | 4131.5 |

## Correctness evidence

- Warm samples reported verified cache hits; first-population samples recorded writes.
- Cached/uncached and sequential/parallel canonical finding projections matched.
- Observed parallel concurrency did not exceed the resolved bound.
- Combined strict/audit ran one policy composition; its two project evaluations
  are initial evaluation plus the required `--ensure-built` reload, not duplicate mode analysis.
- Three sinks changed render/output counters only.
- Validation and preparation failures (both exit 1) are retained but excluded from successful samples.

## Interpretation limits

This is evidence from one declared reference environment, not a universal speed
guarantee, release threshold, or ordinary CI gate. Preflight dominates command
total on this corpus and is reported separately. Deterministic profile
counters—not local timing deltas—are the normative correctness evidence.
