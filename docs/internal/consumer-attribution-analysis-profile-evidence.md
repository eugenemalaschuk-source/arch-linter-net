# Large-solution duration attribution evidence (#461)

## Correction after PR review

The earlier matrix launched every timed child with `--ensure-built`. Its reported `build_state_preflight = 4,301 ms` and `84.7%` share included the real graph build, post-build resolution, receipt creation/verification, and preflight. It was not a measurement of preflight alone, so that dominant-phase attribution is withdrawn.

The refreshed matrix uses the prepared-receipt path requested by #461: one separate `--ensure-built` invocation prepares the build state before the timed scenarios; every measured child uses `--use-prepared-receipts`, which verifies the prepared receipts without restoring or building. The one-time build-preparation invocation has its own wall-clock and inner-command timings in the raw artifact. The refreshed phase figures below replace the mixed measurements.

## Decision

Primary attribution state: **B — ArchLinterNet performs measurable work at scale, but this evidence does not prove a version regression.** Prepared-receipt verification and analysis work repeat with each independent CLI process over unchanged build state. This synthetic current-tree matrix does not identify the cause of any private adopter's wall-clock regression or establish a historical product regression.

In the one-process prepared-receipt scenario, `build_state_preflight` has an 81.0 ms median, or 11.0% of the per-sample inner command median of 734.0 ms. The earlier 4,301 ms / 84.7% result cannot be used as a preflight figure because it included `--ensure-built` work. The measured counters still show repeated per-process graph and analysis work; bounded-parallel dispatch reduces caller-visible batch time while retaining that work.

The normalized self-dogfood evidence in [self-dogfood-ci-governance-evidence.md](self-dogfood-ci-governance-evidence.md) remains a separate aggregate measure: three comparable post-normalization spans were 144.831 s, 90.085 s, and 158.152 s, with a 144.831 s median and 90.085–158.152 s range. Compared with the 204 s baseline, that is a 29.004% reduction, but it remains a **GAP** against the `<=60 s` target. The report-input projection took 41.967–74.374 s across five CLI processes. Those spans show that workflow normalization improved the self-dogfood lane; they do not identify a Core phase or prove an adopter-wide version regression.

Health now measures snapshot preparation, validation, optional evidence binding, debt-gate evaluation, and summary projection when `health --profile` is requested. The existing repository Health CI projection writes that profile to a separate artifact for future normalized-run analysis. The refreshed synthetic matrix below exercises `validate --profile`; it is not a Health CI profile from a post-normalization run. The new Health profile behavior is covered by focused CLI and Core tests.

The evidence supports state B and routes repeated cross-process preparation to its existing owners. It authorizes no guessed Core optimization. Comparable artifact samples plus caller-side timestamps that separate build, container, scheduler, and CI-runner time are still required to establish a historical product regression.

## Public-safe workload and provenance

The explicit `ConsumerAttributionAnalysisProfileBenchmarkHarness` reuses the synthetic `large-multi-host` fixture: eight host projects, two shared libraries, and 30 source files. It builds the fixture once, invokes `--ensure-built --max-parallelism 1` once to prepare product receipts, then runs strict CLI validation with `--use-prepared-receipts --max-parallelism 1` over the unchanged build state. The matrix varies independent process count (`R = 1, 2, 4`) and dispatch mode (sequential or bounded parallel), with ten measured samples per scenario. All 130 measured child processes are recorded as `--use-prepared-receipts` in the raw evidence and are validated by the harness. It also validates successful exit/publication and identical canonical results across processes. No private adopter repository, namespace, workflow URL, topology, or raw log data is included.

The refreshed Release run used macOS 15.8.0, .NET 10.0.10, x64, and six reported logical processors. Fixture build preparation took 4,071.3 ms. The separate one-time `--ensure-built` invocation took 5,656.0 ms outer wall time and 5,175.0 ms inner command time; this contains the graph build and associated resolution/receipt work and is not included in the timed scenario rows. The CLI package was `ArchLinterNet.Cli` `0.1.0-preview.658`; its launched and packaged assembly SHA-256 was `b226b045a0a98f4f8a995508914cbfe268c4fc0e32442f587541ade81b942964`, and the package SHA-256 was `73808f4e4036ba37b316a7e10c5db8205274de6f0389dc4851aa33fd4ecbcd08`. The raw artifact records source commit `f36563ddbab8c47d6ac68e78b910ab92a44ece99` through `ARCH_LINTER_SOURCE_SHA`; binary and package hashes identify the measured Release artifact.

The full profiles and sample distributions are retained in [consumer-attribution-analysis-profile-results.json](consumer-attribution-analysis-profile-results.json), schema `consumer-attribution-evidence/v2`. Refresh them by building the package and running the explicit `ConsumerAttributionAnalysisProfileBenchmarkHarness`; it is excluded from normal test and acceptance runs.

## Observed matrix

Times are milliseconds; each cell is median / p95 over ten valid samples. `Inner` is the sum of profile command totals across processes in a batch; `build_state_preflight` is the sum of that phase and, for these measured runs, represents prepared-receipt verification without restore or graph build; `analysis` is the harness's remaining analysis interval; `outer` is caller-visible batch wall time; `envelope` is the summed local process envelope.

| Scenario | Inner | Receipt verification (`build_state_preflight`) | Analysis | Outer | Envelope |
|---|---:|---:|---:|---:|---:|
| 1 process, sequential | 734.0 / 757.0 | 81.0 / 86.0 | 586.5 / 608.0 | 1,188.7 / 1,217.3 | 449.4 / 477.6 |
| 2 processes, sequential | 1,473.5 / 1,488.0 | 160.5 / 165.0 | 1,178.5 / 1,187.0 | 2,358.6 / 2,389.3 | 883.9 / 902.7 |
| 4 processes, sequential | 2,952.0 / 2,989.0 | 324.0 / 332.0 | 2,355.0 / 2,390.0 | 4,733.6 / 4,797.9 | 1,767.1 / 1,810.6 |
| 2 processes, bounded parallel | 1,486.0 / 1,544.0 | 162.5 / 175.0 | 1,184.5 / 1,225.0 | 1,401.2 / 1,430.2 | 895.0 / 915.4 |
| 4 processes, bounded parallel | 3,101.0 / 3,365.0 | 355.5 / 382.0 | 2,457.5 / 2,699.0 | 1,871.2 / 1,909.5 | 1,844.5 / 1,930.8 |

The counters show the attribution boundary clearly. One process records 10 discovered projects, one project-graph evaluation, one fact-index materialization, one source-scan pass, and two contract executions. Two processes record 20/2/2/2/4; four record 40/4/4/4/8. Every measured process across all scenarios produced the same canonical result digest. Parallel dispatch shortens outer wall time but does not remove repeated inner work.

## Measurement boundaries

| Boundary | Evidence | Interpretation |
|---|---|---|
| Synthetic fixture build | One setup stopwatch before timed samples | Fixture build setup is reported separately from CLI timing. |
| Product `--ensure-built` preparation | One priming invocation; 5,175.0 ms inner command and 5,656.0 ms outer wall time | Contains the real graph build plus post-build resolution and receipt creation/verification. It is reported separately and excluded from the measured scenario rows. |
| Prepared-receipt verification | `build_state_preflight` in children marked `--use-prepared-receipts` | Measures receipt verification without restore or graph build; one-process median is 81.0 ms. |
| ArchLinterNet analysis | Profile phases after preflight and before output | Includes snapshot, index, fact, and contract work exposed by `analysis-profile/v1`. |
| Output | Profile render/staging/stream/commit phases | `--format json` stays constant; output is separated from analysis. |
| Local process envelope | Outer child-process wall time minus profile command total | Includes local `dotnet` startup and redirected-pipe overhead. |
| Container / scheduler / CI runner | Not observable in this repository harness | Requires timestamps from the caller; no product root cause is inferred from aggregate CI time. |

## Result interpretation and routing

- **#502:** reuse the `large-multi-host` process-count shape, `R` variable, and per-process profile/counter envelope in the canonical benchmark foundation.
- **#492/#493:** prepared-analysis reuse remains the owner for cross-process preparation; this evidence measures duplication and does not implement reuse.
- **#655/#675/#503:** this matrix makes no selector/layer, real-MSBuild cache-eligibility, or changed-project advisory claim.
- **#991:** retain the post-normalization self-dogfood gap and use the new Health profile artifact in later normalized runs before attributing aggregate consumer-shaped time to Core.

The matrix is environment-labelled evidence, not a timing SLA. The Windows run in the previous artifact and this macOS run are not compared as a before/after product pair. A version-regression claim requires comparable packed-artifact samples on a controlled environment and consumer-side timestamps that separate caller orchestration from ArchLinterNet's profile boundary.

## OpenSpec

OpenSpec change `profile-health-phase-timings` applies because this work adds opt-in Health phase measurements while preserving the existing profile schema and default behavior. It is archived with the implementation.
