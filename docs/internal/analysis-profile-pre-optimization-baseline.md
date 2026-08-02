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
| Repository state | `feature/374-analysis-profile-v1`, `Debug` configuration, local development build (not CI) |
| Harness | `AnalysisProfileBenchmarkHarness.RunBenchmarkMatrix` (`tests/ArchLinterNet.Core.Tests/AnalysisProfile/`) |
| Fixture | `large-multi-host` (8 synthetic host projects + 2 shared library projects) |

## Method

Every sample is one real `dotnet <ArchLinterNet.Cli.dll> --policy ... --profile <path>` process invocation against an isolated copy of the fixture. `AnalysisMs` is derived from the profile's own `Phases`: the single-mode path's explicit `total` phase, or — for the combined `--mode strict,audit` path, which has no `total` wrapper (`ArchitectureValidationApplicationService.CreateSnapshot` doesn't wrap in `Measure("total")` the way single-mode `Validate` does) — the sum of that profile's top-level (`Indent: 0`) phases. `PreflightMs` is the `build_state_preflight` phase alone, isolating MSBuild-driven `--ensure-built` verification from the rest of ArchLinterNet's own analysis work, per the issue's "separate restore/build/preparation time from ArchLinterNet analysis time" requirement. Every scenario has ten samples; every cold sample uses a separately created never-built fixture copy, so its median and p95 are statistically meaningful.

## Results

| Scenario | n | Median analysis (ms) | p95 analysis (ms) | Median preflight (ms) | Completion status |
|---|---|---|---|---|---|
| 1 — cold process, warm filesystem, strict | 10 | 571.5 | 7206 | 5795.5 | Success |
| 2 — immediate warm strict repeat (no persistent cache — #365) | 10 | 516.5 | 802 | 4749.5 | Success |
| 3 — strict + audit as separate legacy-style processes (paired sum) | 10 | 912 | 1910 | 5835¹ | Success/Success |
| 4 — combined strict+audit from one #363 snapshot | 10 | 509 | 871 | 4807 | Success |
| 5a — one report sink (`--report json=stdout`) | 10 | 483 | 628 | 4776.5 | Success |
| 5b — three report sinks (`--report human/json/sarif`, one analysis) | 10 | 742 | 1074 | 4939.5 | Success |
| 7b — validation-failure completion path | 10 | 582.5 | 936 | 6601 | ValidationFailure |
| 7c — preparation-failure completion path (never built, `--no-restore`, no receipts) | 10 | 409.5 | 940 | 5 | PreparationFailure |

¹ Preflight median for scenario 3's strict-process leg only; the audit-process leg pays its own separate preflight cost on top of this (not summed here — see "Observations").

Scenario 6 ("sequential execution before #408") is not a separate timed row: every scenario above already runs sequentially, since no parallel-scanning capability exists yet. Scenario 7's "success" completion path is already demonstrated by scenarios 1–5.

## Observations

- **The #363 one-snapshot benefit remains visible end to end**: scenario 3 (two separate processes, 912ms combined median analysis time) versus scenario 4 (one process, one snapshot, 509ms) is consistent with serving both modes from one composed snapshot. The deterministic snapshot counters, rather than this environment-sensitive delta, are the normative proof.
- **`--ensure-built` preflight dominates wall-clock time on this fixture** (~4.7–6.6s median per run) regardless of cold/warm state, because `--ensure-built` shells out to `dotnet build` for verification on every invocation against ten projects. This is exactly the kind of cost #365's persistent cache is expected to address, and is why this evidence separates `PreflightMs` from `AnalysisMs` rather than reporting one blended number.
- **Multi-sink output does not repeat analysis**: scenario 5b has a higher machine-local median than 5a, so this evidence does not claim a duration equivalence. Its one-snapshot / sink-count invariant is instead proved by the profile counters and correctness tests; report-rendering and process noise remain observable in this descriptive benchmark.
- **`PeakWorkingSetBytes` is `null` in every sample on this platform** — `System.Diagnostics.Process.PeakWorkingSet64` is a documented no-op returning `0` on macOS rather than throwing; `ValidateCommandHandler.Profile.cs` treats that as "unavailable" and reports `null` explicitly rather than a misleading `0` (see the phase/counter dictionary).

## Non-release statement

This is internal, descriptive baseline evidence only — one developer machine, one run of the harness. It is **not** a universal or hardware-independent performance guarantee, does not gate any release, and does not authorize a version bump. Per the issue's own acceptance criteria, "median/p95 evidence is reproducible and is not presented as a universal speed contract." Re-run `AnalysisProfileBenchmarkHarness.RunBenchmarkMatrix` (see `docs/internal/analysis-profile-dictionary.md`) to refresh this evidence after #365/#408 land, for #409's post-optimization comparison.
