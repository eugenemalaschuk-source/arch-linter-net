# Large-solution duration attribution evidence (#461)

## Decision

Primary attribution state: **B — ArchLinterNet performs measurable work at scale, but this evidence does not prove a version regression.** The refreshed synthetic matrix shows preparation cost and counters multiplying with each independent CLI process over unchanged build state. It does not identify the cause of any private adopter's wall-clock regression or establish a historical product regression.

The one-process inner median is 5,062 ms; `build_state_preflight` is the dominant phase at a 4,301 ms median and 84.7% median per-sample share. With two and four independent processes, aggregate project discovery, graph evaluation, fact-index materialization, source scans, and contract execution scale exactly with process count. Bounded-parallel dispatch reduces caller-visible batch time while retaining the duplicated inner work.

The normalized self-dogfood evidence in [self-dogfood-ci-governance-evidence.md](self-dogfood-ci-governance-evidence.md) remains a separate aggregate measure: three comparable post-normalization spans were 144.831 s, 90.085 s, and 158.152 s, with a 144.831 s median and 90.085–158.152 s range. Compared with the 204 s baseline, that is a 29.004% reduction, but it remains a **GAP** against the `<=60 s` target. The report-input projection took 41.967–74.374 s across five CLI processes. Those spans show that workflow normalization improved the self-dogfood lane; they do not identify a Core phase or prove an adopter-wide version regression.

Health now measures snapshot preparation, validation, optional evidence binding, debt-gate evaluation, and summary projection when `health --profile` is requested. The existing repository Health CI projection writes that profile to a separate artifact for future normalized-run analysis. The refreshed synthetic matrix below exercises `validate --profile`; it is not a Health CI profile from a post-normalization run. The new Health profile behavior is covered by focused CLI and Core tests.

The evidence supports state B and routes repeated cross-process preparation to its existing owners. It authorizes no guessed Core optimization. Comparable artifact samples plus caller-side timestamps that separate build, container, scheduler, and CI-runner time are still required to establish a historical product regression.

## Public-safe workload and provenance

The explicit `ConsumerAttributionAnalysisProfileBenchmarkHarness` reuses the synthetic `large-multi-host` fixture: eight host projects, two shared libraries, and 30 source files. It builds the fixture once, then runs strict CLI validation with `--ensure-built --max-parallelism 1` over unchanged build state. The matrix varies independent process count (`R = 1, 2, 4`) and dispatch mode (sequential or bounded parallel), with ten measured samples per scenario. It validates successful exit/publication and identical canonical results across processes. No private adopter repository, namespace, workflow URL, topology, or raw log data is included.

The refreshed Release run used macOS 15.8.0, .NET 10.0.10, x64, and six reported logical processors. Fixture build preparation took 4,036.5 ms. The CLI package was `ArchLinterNet.Cli` `0.1.0-preview.658`; its launched and packaged assembly SHA-256 was `9742a414092bad54d6e695a3ff9b7cdb0c1756f1800f817865be054ad59f1591`, and the package SHA-256 was `e1ad4ff90408b19000cce69d7c1c37a2db2b631cefe8bbdaee7556923e5e91ef`. The raw artifact's `SourceCommit` is `unknown` because `ARCH_LINTER_SOURCE_SHA` was not supplied; binary and package hashes identify the measured Release artifact.

The full profiles and sample distributions are retained in [consumer-attribution-analysis-profile-results.json](consumer-attribution-analysis-profile-results.json). Refresh them by building the package and running the explicit `ConsumerAttributionAnalysisProfileBenchmarkHarness`; it is excluded from normal test and acceptance runs.

## Observed matrix

Times are milliseconds; each cell is median / p95 over ten valid samples. `Inner` is the sum of profile command totals across processes in a batch; `build-state preflight` is the sum of that phase; `analysis` is the harness's remaining analysis interval; `outer` is caller-visible batch wall time; `envelope` is the summed local process envelope.

| Scenario | Inner | `build_state_preflight` | Analysis | Outer | Envelope |
|---|---:|---:|---:|---:|---:|
| 1 process, sequential | 5,062 / 7,814 | 4,301 / 6,588 | 667 / 1,114 | 5,519 / 8,350 | 464.7 / 637.1 |
| 2 processes, sequential | 9,897.5 / 10,073 | 8,370.5 / 8,552 | 1,319.5 / 1,373 | 10,821.1 / 10,987.7 | 915.6 / 931.9 |
| 4 processes, sequential | 19,602.5 / 19,681 | 16,593.5 / 16,692 | 2,626 / 2,645 | 21,344.7 / 21,464.3 | 1,765.6 / 1,818.7 |
| 2 processes, bounded parallel | 10,280 / 11,273 | 8,703.5 / 9,501 | 1,359 / 1,549 | 5,810.3 / 6,267.3 | 907.4 / 951.3 |
| 4 processes, bounded parallel | 24,666.5 / 27,748 | 21,334 / 24,256 | 2,928.5 / 3,220 | 7,441.0 / 8,006.2 | 1,924.6 / 2,033.7 |

The counters show the attribution boundary clearly. One process records 10 discovered projects, one project-graph evaluation, one fact-index materialization, one source-scan pass, and two contract executions. Two processes record 20/2/2/2/4; four record 40/4/4/4/8. Every measured process across all scenarios produced the same canonical result digest. Parallel dispatch shortens outer wall time but does not remove repeated inner work.

## Measurement boundaries

| Boundary | Evidence | Interpretation |
|---|---|---|
| Fixture build / restore | One setup stopwatch before timed samples | Build setup is reported separately from timed CLI validation. |
| ArchLinterNet preparation | `build_state_preflight`, `post_ensure_built_preflight`, and preparation counters | Product preparation repeats for each independent CLI process over the same build state. |
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
