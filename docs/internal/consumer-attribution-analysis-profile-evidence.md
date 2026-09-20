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
`build_state_preflight` is 2,563.5 ms of the 3,131.5 ms 1-process inner median
(81.9%); the scenario medians keep the same phase at about 81.9–82.4% of inner
time.

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
`d69e135de4f8241f10fcb6c21d31b19b73c949ea` in Release configuration on Windows
10.0.26200, .NET 10.0.12, x64, with 16 logical processors. The fixture contained
10 projects and 30 source files; its one-time build setup took 3,508.0 ms. The
raw artifact is 2,558,192 bytes and retains every process profile.

## Provenance and equivalence

The evidence records the Release binary actually launched by every child process:
`CliFileVersion=0.1.0.0` and
`CliAssemblySha256=960184941645b0eae40ad4bd466cbbc898fb8f2c36ac8afebef3a5bdc5de7f6c`.
The harness opens the selected package, hashes its
`tools/net10.0/any/ArchLinterNet.Cli.dll` entry, and refuses to record evidence
unless that hash equals the launched DLL. The verified package assembly hash is
also recorded explicitly as `CliPackageAssemblySha256`.
It also records the matching package identity:
`ArchLinterNet.Cli` version `0.1.0-preview.658`, package SHA-256
`19803137bdfd2470df68e230203f25c1e857eb3ed8d8b13ff12e0651d418d175`;
the embedded assembly SHA-256 is
`960184941645b0eae40ad4bd466cbbc898fb8f2c36ac8afebef3a5bdc5de7f6c`.

Canonical equivalence includes the current JSON contract's
`cycle_diagnostics`, `coverage_summary`, `preflight_diagnostics`, and
`source_set_expansion` fields (alongside the remaining result and classification
sections); it is not silently reduced to a nonexistent `cycle_findings` property.
The harness observes NUnit's cooperative `TestContext.CurrentContext.CancellationToken`
while building the fixture and while waiting for each child process, links it to
300-second timeouts, kills the entire process tree on cancellation, and uses
bounded cleanup.

## Observed matrix

Times are milliseconds; each cell is median / p95 over ten valid samples.
`Inner` is the sum of profile command totals across the processes in a batch,
`build_state_preflight` is the corresponding dominant phase, `analysis` excludes
preflight and output, `outer` is the whole local batch wall time, and `envelope`
is the summed local process envelope.

| Scenario | Inner | build_state_preflight | Analysis | Outer | Envelope |
|---|---:|---:|---:|---:|---:|
| 1 process, sequential | 3131.5 / 3316.0 | 2563.5 / 2704.0 | 467.5 / 544.0 | 3334.5 / 3539.5 | 201.6 / 216.0 |
| 2 processes, sequential | 6284.5 / 6604.0 | 5149.0 / 5417.0 | 951.0 / 980.0 | 6705.8 / 7038.9 | 398.6 / 420.2 |
| 4 processes, sequential | 12844.5 / 13050.0 | 10582.0 / 10789.0 | 1902.5 / 1928.0 | 13698.4 / 13886.1 | 811.4 / 831.9 |
| 2 processes, bounded parallel | 6234.0 / 6460.0 | 5113.0 / 5279.0 | 936.5 / 974.0 | 3331.4 / 3439.7 | 406.4 / 420.2 |
| 4 processes, bounded parallel | 13408.5 / 16188.0 | 11033.0 / 13596.0 | 1966.5 / 2259.0 | 3632.9 / 4306.6 | 841.6 / 998.1 |

The deterministic counters show the attribution boundary more clearly than the
wall clock. A single process records 10 discovered projects, one project-graph
evaluation, one fact-index materialization, and two contract executions. The
same counters are exactly 20/2/2/4 for two processes and 40/4/4/8 for four
processes in both dispatch modes. Every process produced the same canonical
result digest:

`41a6639f73057c2dd682535e2383aefadd4e7ba4c92aa7b0156e8bb749b0c5cb`

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
