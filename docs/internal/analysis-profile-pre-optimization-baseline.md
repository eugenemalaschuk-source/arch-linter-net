# analysis-profile/v1 pre-optimization baseline evidence

## Scope

This evidence records the pre-cache/pre-parallel baseline required by issue #374, ahead of #365 (persistent cache) and #408 (bounded parallel scanning) implementation and #409's post-optimization comparison. It exercises the `large-multi-host` fixture (`tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/large-multi-host/`, ten synthetic projects) through the real `arch-linter-net` CLI over real OS processes, using `--profile` to source every timing figure below directly from `analysis-profile/v1` documents — not hand-timed wall clocks.

## Observed execution environment

| Field | Observed value |
|---|---|
| Platform | macOS 15.7.7, x86_64 |
| CPU | Intel(R) Core(TM) i5-8500B CPU @ 3.00GHz, 6 cores |
| Memory | 32 GB |
| .NET SDK | 10.0.302 |
| .NET runtime | 10.0.10 (host) |
| Repository state | Product instrumentation at `cf82e4e` on `feature/374-analysis-profile-v1`, `Debug` configuration, local development build (not CI); this evidence refresh adds only the benchmark/evidence changes recorded with it |
| Harness | `AnalysisProfileBenchmarkHarness.RunBenchmarkMatrix` (`tests/ArchLinterNet.Core.Tests/AnalysisProfile/`) |
| Fixture | `large-multi-host` (8 synthetic host projects + 2 shared library projects) |

## Method

Every sample is one real `dotnet <ArchLinterNet.Cli.dll> --policy ... --profile <path>` process invocation against an isolated copy of the fixture. Before statistics, the harness checks each measured and priming profile's `CompletionStatus`, CLI exit category, and `Output.OutputFailed` value: the declared success paths require `Success`/`0`/`false`; validation and preparation failures require their matching status with exit `1` and `false`. A runtime/output failure (exit `2` or `OutputFailed: true`) is therefore rejected rather than counted as a successful sample.

Timing boundaries are uniform across single and combined modes. `PreflightMs` is `build_state_preflight` alone. `Analysis-only` excludes preflight plus every rendering/publication phase: `render_human`, `render_json`, `render_sarif`, `output_staging`, `output_stream_write`, and `output_commit`. `Output` is the sum of those rendering/publication phases. `Command total` includes analysis-only, preflight, and output: for single mode it is the explicit `total` phase plus output, and for combined mode it is the top-level (`Indent: 0`) phase sum. This makes scenarios 3–5 directly comparable while preserving output work as its own measurement.

Every scenario has ten measured samples; every cold sample uses a separately created never-built fixture copy, so its median and p95 are statistically meaningful. The checked-in [raw profile artifact](analysis-profile-pre-optimization-baseline-results.json) retains all 95 process profiles: 90 measured profiles used for statistics and 5 validated priming profiles, including phase `ProcessorTimeMs`, measurements, `Output`, deterministic counters, exit code, and derived timing boundaries. This run's artifact is 1,302,047 bytes with SHA-256 `bd2435fe6bc94fb9c717277c5d84228baa611b049b2c1b23d8eb12dd15610635`.

## Results

| Scenario | n | Analysis-only median / p95 (ms) | Output median / p95 (ms) | Command-total median / p95 (ms) | Preflight median (ms) | Completion status |
|---|---:|---:|---:|---:|---:|---|
| 1 — cold process, warm filesystem, strict | 10 | 356.5 / 551 | 20 / 47 | 4188 / 8783 | 3799 | Success |
| 2 — immediate warm strict repeat (no persistent cache — #365) | 10 | 344 / 364 | 21 / 21 | 3587 / 3701 | 3219 | Success |
| 3 — strict + audit as separate legacy-style processes (paired sum) | 10 | 687.5 / 708 | 41 / 43 | 7086.5 / 7200 | 6361.5 | Success/Success |
| 4 — combined strict+audit from one #363 snapshot | 10 | 329.5 / 345 | 20 / 21 | 3535.5 / 3624 | 3190.5 | Success |
| 5a — one report sink (`--report json=stdout`) | 10 | 343.5 / 367 | 45 / 51 | 3564 / 3664 | 3169.5 | Success |
| 5b — three report sinks (`--report human/json/sarif`, one analysis) | 10 | 348 / 382 | 55 / 58 | 3554.5 / 3882 | 3159.5 | Success |
| 7b — validation-failure completion path | 10 | 377.5 / 388 | 28 / 29 | 3599.5 / 3847 | 3196 | ValidationFailure |
| 7c — preparation-failure completion path (never built, `--no-restore`, no receipts) | 10 | 288.5 / 298 | 39 / 42 | 331.5 / 341 | 4 | PreparationFailure |

Scenario 6 ("sequential execution before #408") is not a separate timed row: every scenario above already runs sequentially, since no parallel-scanning capability exists yet. Scenario 7's "success" completion path is already demonstrated by scenarios 1–5.

## Observations

- **The #363 one-snapshot benefit remains visible end to end**: scenario 3's paired separate processes have 687.5ms median analysis-only time and 7086.5ms median command total, versus scenario 4's 329.5ms and 3535.5ms. The deterministic snapshot counters, rather than these environment-sensitive deltas, are the normative proof.
- **`--ensure-built` preflight dominates command-total time on this fixture** (~3.2–3.8s median per process) because it shells out to `dotnet build` for verification on every invocation against ten projects. This is exactly the kind of cost #365's persistent cache is expected to address, and why this evidence reports preflight separately rather than blending it into analysis-only time.
- **Multi-sink output does not repeat analysis**: scenarios 5a/5b have similar analysis-only medians (343.5/348ms) but separately expose 45/55ms median rendering/publication work. Their one-snapshot / sink-count invariant is proved by the raw profile counters and correctness tests; these descriptive measurements make output work observable without treating local timing as a duration contract.
- **`PeakWorkingSetBytes` is `null` in every sample on this platform** — `System.Diagnostics.Process.PeakWorkingSet64` is a documented no-op returning `0` on macOS rather than throwing; `ValidateCommandHandler.Profile.cs` treats that as "unavailable" and reports `null` explicitly rather than a misleading `0` (see the phase/counter dictionary).

## Non-release statement

This is internal, descriptive baseline evidence only — one developer machine, one run of the harness. It is **not** a universal or hardware-independent performance guarantee, does not gate any release, and does not authorize a version bump. Per the issue's own acceptance criteria, "median/p95 evidence is reproducible and is not presented as a universal speed contract." Re-run `AnalysisProfileBenchmarkHarness.RunBenchmarkMatrix` (see `docs/internal/analysis-profile-dictionary.md`) to refresh this evidence after #365/#408 land, for #409's post-optimization comparison.
