# Prepared-analysis evidence contract (#493)

This page is the internal decision contract for issue #493. It defines what a
current, consumer-shaped measurement must prove before a persisted
prepared-analysis implementation can be authorized. It is synthetic and
public-safe: examples use only workload IDs, command-family names, counters,
digests, and normalized tool/runtime identity. It must not contain adopter
names, repository URLs, private topology, or raw private logs.

The contract is pre-implementation evidence. A prepared-state label or expected
effect is not a claim that a prepared store, format, authorization protocol, or
CLI surface already exists.

## Consumer shapes and preparation boundary

Measure the same policy semantics in both supported candidate shapes:

| Shape | What the fixture owns | What the analysis sample means |
|---|---|---|
| `real_ms_build` | Restore/build of the synthetic candidate and its build receipt. | The profile may include build-state preflight and post-build reload; fixture build setup remains separately attributable. |
| `staged_assemblies` | External compilation before analysis, exact DLL copy/hash verification, and a receipt for each staged input. | Analysis starts from the verified staged assemblies; external compilation is not silently charged to analyzer preparation. |

The staged candidate must be derived from the same synthetic inputs as the
real-MSBuild candidate. Equivalent candidates must retain matching canonical
findings, identity, ordering, completion, and exit semantics. A mismatch fails
the comparison closed rather than producing a reuse claim.

## Current full-governance command mix

The workload manifest and every evidence run must identify the command family
and process/projection boundary. The representative current mix is:

- strict validation;
- audit validation;
- no-new-debt;
- Architecture Health;
- current-side change snapshot.

Topology, measure, baseline/reference verification, and public-API comparison
are optional projections. Add each only when the declared workflow invokes it
and retains its raw profile, deterministic counters, command identity, exit
code, and publication status. The evidence must not describe an unmeasured
projection as part of the workflow.

## Three-way comparison

The decision compares the same candidate state in three forms:

1. Independent one-shot processes. Each process materializes its own analysis
   snapshot and retains its complete `analysis-profile/v1` payload. Retain
   deterministic preparation/fact counters by candidate process ordinal for
   attribution, but use measured elapsed durations for cost decisions.
2. One-process multi-projection execution. Safe read-only projections share one
   immutable `ArchitectureAnalysisSnapshot`/session. Record which projections
   share preparation and which remain process-bound; sharing is not assumed for
   command families that require a separate lifetime or revision.
3. Persisted prepared-state expected effect. Model the cold prepare and each
   consumer load/authorization cost from measured duration evidence in the
   same unit. This is an expected-effect model, not a prepared implementation
   result.

For all three forms, compare canonical findings, canonical identity, ordering,
completion status, exit semantics, and publication outcome. Wall-clock, CPU,
allocation, and memory values are environment-labelled observations. Missing
resource measurements are explicit unavailable/not-applicable values, never
fabricated zeros.

## Candidate versus base/reference attribution

When a change snapshot needs both revisions, run and label them separately:

- candidate: the revision whose reusable preparation and expected effect are
  under consideration;
- base/reference: the comparison revision used to classify the change.

Base/reference policy composition, project discovery, assembly loads, fact
materialization, and contract counters do not count toward candidate reuse
savings. They are also excluded from the candidate break-even point and the
candidate whole-workflow upper bound. A base result may be equivalent or
different by design; it still cannot be presented as reusable candidate work.

## Phase, counter, cache, and prepared boundaries

Use the `analysis-profile/v1` dictionary as the inner boundary. At minimum,
interpret the following separately:

| Boundary | Include | Do not infer |
|---|---|---|
| Build/setup | Fixture restore/build or staged-input verification, with its receipt and outer timing. | A product analyzer optimization. |
| Product preparation | `policy_composition`, `load_and_setup`, `build_state_preflight`, `post_ensure_built_reload`, assembly resolution, and snapshot/fact materialization. | A single undifferentiated “analysis” number. |
| Deterministic fact work | Project/assembly/source/fact/contract counters and per-family phases. | That a base/reference count is candidate reuse. |
| Projection/output | Contract result, canonical digest, rendering, staging, stream, commit, completion, and exit/publication status. | That equal timing proves semantic equivalence. |

Keep `run.cache_mode` and `run.prepared_state_mode` independent. Compare cache
disabled, miss, and exact-request hit where the workflow supports them, and
separately compare unprepared, one-process-shared, and expected persisted
prepared states. Work avoided by an `analysis-cache/v1` exact-request hit is
cache-avoidable work, not prepared-state benefit.

## Expected-effect contract

The evidence must record, for the candidate only:

- representative process/consumer count and command mix `R`;
- repeated-work share `p`;
- deterministic candidate prepared-boundary counts;
- cold prepare duration `C` in milliseconds;
- representative per-consumer load/authorization duration `L` in
  milliseconds, plus a repeated measured serialized-state load/authorization
  proxy for every small, medium, and large scale point;
- small, medium, and large expected effect derived from measured scale points
  using each point's own `L` rather than borrowing a medium-workload proxy;
- the solution dimensions and command count represented by each scale point;
- first crossover `R`, or an explicit no-crossover result;
- whole-workflow upper bound;
- storage, I/O, allocation, and memory trade-offs;
- issue-specific success threshold, kill criterion, and uncertainty.

For a one-shot preparation estimate `P`, the comparison is:

```text
independent one-shot = R × P + U
prepared-state model  = C + R × L + U
```

Here `U` is unavoidable consume/projection work that is paid once by the
workflow boundary in either model. It therefore cancels from the preparation
crossover, which is evaluated as `C + R × L < R × P`. For each representative
family, `P` is derived from the independent wall-clock duration minus the
matching one-process projection duration; `U` contains those projection
durations exactly once. `C`, `P`, `L`, and `U` must all be elapsed-duration
values in milliseconds; deterministic counter cardinalities are never added,
divided, or compared as if they were time. `L` must be measured independently
from `U`: before a persisted store exists, the benchmark uses a repeated
in-memory serialized-state load/authorization proxy and records its
uncertainty/basis explicitly.
`SelectedAssemblyCount` and projection counters remain attribution evidence,
not cost units, and projection duration must not be reused as `L` when it is
also included in `U` or in the one-process alternative.

If `P <= L`, there is no persisted-state crossover under this model because
`U` is non-negative. If `P > L`, report the smallest representative `R` where
the persisted model is cheaper. Do not count a cache hit twice, and do not
authorize outcome A from process count alone. The current #493 gate also requires a numeric materiality
threshold: persisted reuse must be at least 10% cheaper than the measured
representative one-process alternative, with the ratio derived from the two
measured costs rather than copied from prose. The three scale points must come
from the same disabled-cache command-family matrix, and each point must
measure its own independent `L`. Otherwise the decision remains
non-authorizing until an authoritative scaling artifact is linked.

## Decision and routing

The evidence records one of the following decision routes, with a reason:

- **A — authorize the prepared-analysis lane:** only when the one-process
  alternative is insufficient and the measured expected effect, materiality
  threshold, crossover, scale matrix, and resource trade-offs meet the
  issue-specific contract;
- **B — defer:** the current evidence does not show material distinct value,
  the one-process alternative is sufficient, or the required measurements are
  not yet available;
- **C — route elsewhere:** the observation belongs to another benchmark,
  cache, build, or workflow owner, or the suspected repeated work was not
  reproduced.

No route authorizes unrelated product changes. The checked-in artifact must
remain marked as pre-implementation and must retain raw profiles or explicit
unavailable measurements.

## Refresh and validation

The explicit issue-specific benchmark harness is the only refresh authority for
the machine-readable #493 artifact. Invoke its test entry point directly after
the harness is implemented; do not add it to `make test` or `make acceptance`.
The normal gate should cover deterministic schema/serialization, attribution,
canonical-equivalence, privacy-safe identity, and break-even/effect contract
tests only. Before reviewing a refreshed artifact, run:

```text
openspec validate --all
make lint-docs
```

Inspect the diff for fabricated measurements, private identifiers, stale raw
profiles, base samples included in candidate savings, or a command listed that
the manifest did not invoke.
