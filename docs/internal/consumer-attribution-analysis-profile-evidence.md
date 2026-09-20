# Consumer-shaped duration attribution evidence (#461)

## Decision

Primary attribution state: **B — ArchLinterNet is measurable at scale, but no
version regression is proven by this run.**

The public-safe reproducer shows that independent CLI processes over one
unchanged synthetic build each perform their own profiled preparation and
analysis work. That establishes the ArchLinterNet inner boundary and makes
duplicated per-process work visible. It does not turn one synthetic current-tree
measurement into a claim about a private adopter's regression or a universal
performance contract.

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
`f696881365afad743398b24cb066d4d8b2b9a0a4` in Debug configuration on Windows
10.0.26200, .NET 10.0.12, x64, with 16 logical processors. The fixture contained
10 projects and 30 source files; its one-time build setup took 3,304.2 ms. The
raw artifact is 2,548,988 bytes and retains every process profile.

## Observed matrix

Times are milliseconds; each cell is median / p95 over ten valid samples.
`Inner` is the sum of profile command totals across the processes in a batch,
`analysis` excludes preflight and output, `outer` is the whole local batch wall
time, and `envelope` is the summed local process envelope.

| Scenario | Inner | Analysis | Outer | Envelope |
|---|---:|---:|---:|---:|
| 1 process, sequential | 3171.0 / 3231.0 | 457.5 / 529.0 | 3387.2 / 3439.3 | 203.5 |
| 2 processes, sequential | 7182.5 / 11578.0 | 1042.0 / 1385.0 | 7693.1 / 12220.0 | 468.4 |
| 4 processes, sequential | 12812.0 / 16235.0 | 1861.5 / 2155.0 | 13700.8 / 17115.7 | 854.8 |
| 2 processes, bounded parallel | 6628.0 / 7648.0 | 932.0 / 1081.0 | 3562.9 / 4086.2 | 416.8 |
| 4 processes, bounded parallel | 13799.5 / 14482.0 | 1924.5 / 2000.0 | 3734.6 / 3880.0 | 870.7 |

The deterministic counters show the attribution boundary more clearly than the
wall clock. A single process records 10 discovered projects, one project-graph
evaluation, one fact-index materialization, and two contract executions. The
same counters are exactly 20/2/2/4 for two processes and 40/4/4/8 for four
processes in both dispatch modes. Every process produced the same canonical
result digest:

`682de1530e362164ec7be74ebd7e07271b903b0331678127255baeac7d9cfcd4`

Thus bounded parallel dispatch hides duplicated inner work behind a relatively
flat outer wall time, while the per-process profiles and aggregate counters
show that the work was still performed four times. This is sufficient to route
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
