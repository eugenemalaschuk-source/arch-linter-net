# Consumer-shaped duration attribution evidence (#461)

## Decision

Primary attribution state: **B — ArchLinterNet is measurable at scale, but no
version regression is proven by this run.** This evidence does not close #461:
the synthetic current-tree matrix still lacks consumer-side timestamps and
comparable-version samples for attribution of the original CI duration spike.

The public-safe reproducer shows that independent CLI processes over one
unchanged synthetic build each perform their own profiled preparation and
analysis work. That establishes the ArchLinterNet inner boundary and makes
duplicated per-process work visible. It does not turn one synthetic current-tree
measurement into a claim about a private adopter's regression or a universal
performance contract. The measured dominant phase is nevertheless concrete:
`build_state_preflight` is 2,615.5 ms of the 3,205.0 ms 1-process inner median
(81.8%); the same phase remains about 81.2–82.1% across the other scenarios.

The complete raw profiles and sample distributions are retained in
[`consumer-attribution-analysis-profile-results.json`](consumer-attribution-analysis-profile-results.json).
Run the explicit `ConsumerAttributionAnalysisProfileBenchmarkHarness` to refresh
the artifact; it is excluded from normal test and acceptance runs.

## Public-safe workload

The harness reuses `large-multi-host`: eight synthetic host projects and two
synthetic shared libraries. It builds the fixture once, then runs strict CLI
validation with `--ensure-built --max-parallelism 1` over that unchanged build
state. The independent-process variable is `R = 1, 2, 4`; each size is measured
sequentially and with bounded parallel dispatch. Ten valid samples are retained
per scenario. No private repository, namespace, topology, workflow URL, or raw
consumer log is committed.

The matrix is intentionally orthogonal to the existing post-optimization
matrix: #409 already covers cache-disabled/population/hit, strict/audit versus
combined execution, and within-process bounded assembly/source scanning. This
artifact isolates repeated work caused by independent process count.

The checked-in run was produced from source commit
`3ecc7220a125fe8c140fa025de70686f59845e41` in Debug configuration on Windows
10.0.26200, .NET 10.0.12, x64, with 16 logical processors. The fixture contained
10 projects and 30 source files; its one-time build setup took 3,130.0 ms. The
raw artifact is 2,558,213 bytes and retains every process profile.

## Provenance and equivalence

The evidence records the binary actually launched by every child process:
`CliFileVersion=0.1.0.0` and
`CliAssemblySha256=96b37feaaae4de01d252a03aa617a46eb256392b0c95ebfb89cedd1fc1a15ceb`.
It also records the matching package identity:
`ArchLinterNet.Cli` version `0.1.0-preview.658`, package SHA-256
`4568a4aaa351237ad4f774b06277fad2bfc9d8cb5e0d0974ff93993dc9d67312`.

Canonical equivalence includes the current JSON contract's `cycle_diagnostics`
field (alongside paths, violations, coverage, policy-consistency, and
classification fields); it is not silently reduced to a nonexistent
`cycle_findings` property. The harness also observes NUnit's cooperative
`TestContext.CurrentContext.CancellationToken`, links it to each 300-second
process timeout, kills the entire child process tree on cancellation, and uses
bounded cleanup.

## Observed matrix

Times are milliseconds; each cell is median / p95 over ten valid samples.
`Inner` is the sum of profile command totals across the processes in a batch,
`build_state_preflight` is the corresponding dominant phase, `analysis` excludes
preflight and output, `outer` is the whole local batch wall time, and `envelope`
is the summed local process envelope.

| Scenario | Inner | build_state_preflight | Analysis | Outer | Envelope |
|---|---:|---:|---:|---:|---:|
| 1 process, sequential | 3205.0 / 3287.0 | 2615.5 / 2691.0 | 487.5 / 539.0 | 3423.3 / 3507.8 | 212.0 |
| 2 processes, sequential | 8256.5 / 13604.0 | 6586.0 / 11851.0 | 1287.5 / 1485.0 | 9086.4 / 14293.4 | 571.4 |
| 4 processes, sequential | 12942.0 / 16454.0 | 10532.0 / 13390.0 | 2011.0 / 2495.0 | 13847.7 / 17623.0 | 875.4 |
| 2 processes, bounded parallel | 6820.5 / 7241.0 | 5563.5 / 5894.0 | 1044.0 / 1139.0 | 3645.6 / 3902.9 | 438.2 |
| 4 processes, bounded parallel | 14187.0 / 14779.0 | 11698.5 / 11878.0 | 2062.5 / 2452.0 | 3832.0 / 3969.4 | 881.5 |

The deterministic counters show the attribution boundary more clearly than the
wall clock. A single process records 10 discovered projects, one project-graph
evaluation, one fact-index materialization, and two contract executions. The
same counters are exactly 20/2/2/4 for two processes and 40/4/4/8 for four
processes in both dispatch modes. Every process produced the same canonical
result digest:

`1aa64858ba6fc0dad28ca0e8fb3e9e446389188294a791a6a3a10702e9069a8f`

Thus bounded parallel dispatch hides duplicated inner work behind a relatively
flat outer wall time, while the per-process profiles and aggregate counters
show that the work was still performed four times. The dominant phase is
`build_state_preflight`, not an undifferentiated analysis bucket. This is sufficient to route
the repeated-process shape to #502 and the prepared-analysis decision to
#492/#493; it is not sufficient to claim a historical regression.

## Boundaries and measurements

| Boundary | Evidence | Interpretation |
|---|---|---|
| Fixture build / restore | One setup stopwatch before timed samples | Build setup is reported separately; it is not attributed to the CLI validation inner work. |
| ArchLinterNet preparation | `build_state_preflight` and `post_ensure_built_preflight` phases | Includes the product's build-state/preflight work for the unchanged build. |
| ArchLinterNet analysis | Profile phase totals after preflight and before output | Includes snapshot/index/fact/contract work exposed by `analysis-profile/v1`. |
| Output | Profile render/staging/stream/commit phases | `--format json` is kept constant; output is not confused with analysis. |
| Local process envelope | Outer child-process wall time minus profile command total | Includes local `dotnet` startup and redirected-pipe overhead. |
| Container / scheduler / CI runner | Not observable from this repository harness | Requires timestamps from the caller; no product root cause is asserted from aggregate CI time. |

Each process retains deterministic project/assembly/source/fact/contract
counters, measurements, phases, completion status, exit category, publication
status, and a canonical result digest. Batch totals are sums across processes;
the outer batch wall time remains the caller-visible dispatch measurement. A
matching digest across all processes protects canonical findings and ordering
while the workload is repeated.

## Result interpretation and routing

The process-count matrix is evidence of repeated inner work, not an optimization
authorization. The current result is routed as follows:

- **#502:** reuse the `large-multi-host` process-count shape, `R` variable, and
  per-process profile/counter envelope in the canonical very-large-solution
  benchmark foundation.
- **#492/#493:** decide whether verified prepared analysis should remove repeated
  cross-process preparation; this issue does not implement persisted prepared
  state.
- **#655:** no selector/layer/reachability hypothesis is accepted or rejected
  here because the current matrix does not vary those dimensions.
- **#675:** cache eligibility is disabled and is not evaluated here.
- **#503:** no changed-project/file advisory behavior is exercised.

No new optimization issue is created: the measured repeated-process boundary is
already owned by the prepared-analysis lane and the reusable benchmark lane.

## Structural-stabilization and limitations

The evidence is generated from the current post-v0.8 main-line tree. It does
not reuse pre-#772/#784 timings as final release evidence. If structural cleanup
changes the measured preparation or fact path, rerun this matrix before using
the result for a v0.9 implementation decision.

The artifact is environment-labelled, not a timing SLA. A version-regression
claim requires comparable packed-artifact samples from the relevant versions
and consumer-side timestamps that separate runner/container/build orchestration
from ArchLinterNet's profile boundary.

## OpenSpec

OpenSpec: not applicable. This change adds an explicitly invoked test harness
and internal evidence only; it changes no product behavior, public API, policy
semantics, schema, cache trust boundary, or documented user guarantee.
