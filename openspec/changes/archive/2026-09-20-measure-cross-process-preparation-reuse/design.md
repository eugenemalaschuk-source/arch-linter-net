## Context

The existing `ConsumerAttributionAnalysisProfileBenchmarkHarness` launches
independent strict-validation CLI processes over the `large-multi-host`
fixture. The #987 benchmark foundation already owns deterministic workload
identity and `benchmark-evidence/v1`, while `ArchitectureAnalysisSnapshot`
already exposes the safe in-process reuse seam for evaluation and metric
projections. The missing evidence is the current v0.8 command mix and a
decision-quality comparison between process boundaries and one immutable
snapshot.

## Goals / Non-Goals

**Goals:**

- Add an explicit manual benchmark matrix for real-MSBuild/receipt-backed and
  prebuilt/staged-assembly consumer shapes.
- Measure current candidate projections, process-level preparation, in-process
  snapshot reuse, base-revision separation, cache modes, and canonical result
  equivalence.
- Produce a public-safe, checked-in #493 decision artifact with a quantitative
  expected-effect model before any prepared-state implementation.
- Reuse the #987 workload/evidence contracts and keep deterministic contract
  tests in the normal suite.

**Non-Goals:**

- No persisted prepared-analysis store, authorization protocol, cache format,
  CLI command, or product runtime optimization.
- No private adopter reproduction data, raw logs, or hardware-independent time
  threshold.
- No replacement for #502's corpus or #461's incident-attribution evidence.

## Decisions

### Use one test-only harness with two execution layers

The harness will use child CLI processes for independent one-shot command
measurements and the existing Core `ArchitectureAnalysisSnapshot` seam for the
one-process projection comparison. This keeps the comparison honest: process
startup and independent setup remain visible, while safe in-process sharing is
measured without inventing a public CLI surface. A projection descriptor records
which command families are executed by each layer and which require separate
processes.

Alternative rejected: treating `--mode strict,audit` as the whole one-process
alternative. Combined validation proves only mode sharing and cannot cover
metrics, topology, change snapshots, or the current governance boundary.

### Reuse the existing adoption fixture and add a staged copy derived from it

The real-MSBuild archetype uses the checked-in synthetic `large-multi-host`
fixture and one verified build. The staged archetype copies the same candidate
inputs and exact build outputs into a staged evidence root, then runs the same
policy against the staged assemblies. Evidence names the preparation boundary
instead of comparing unrelated topologies.

Alternative rejected: a second benchmark-only project generator. #987 already
owns synthetic topology and workload identity, and a duplicate fixture would
make #502/#493 results incomparable.

### Model the command mix declaratively

The harness defines a small immutable workflow manifest containing required
strict/audit projections, no-new-debt and health projections, current-side
change snapshot, and optional baseline/public-API/topology/measure projections.
Each command is run only when its fixture inputs are available. The manifest
also marks candidate versus base revision and independent-process versus
in-process execution. This prevents a benchmark description from silently
claiming commands that were not measured.

### Make deterministic work the decision authority

Per-process profile counters, snapshot counters, canonical result digests, and
projection equivalence are the durable gate. Wall-clock, CPU, allocation, and
memory measurements are retained with explicit availability/reason fields and
are used only to estimate effect and trade-offs. The expected-effect model
computes `R × one-shot` versus `prepare + R × load/authorize` and records
break-even or no-crossover rather than assuming process count proves value.

### Keep evidence schema-compatible and privacy-safe

The new result document composes the existing `benchmark-evidence/v1` envelope
and stores raw `analysis-profile/v1` payloads without modifying either schema.
Only synthetic fixture IDs, command-family IDs, counts, normalized digests, and
tool/runtime identity are committed. Base-revision work is separately labeled
and excluded from candidate savings.

## Risks / Trade-offs

- [Risk] A staged fixture may accidentally use different assemblies than the
  real-MSBuild fixture → verify exact artifact hashes and canonical result
  equivalence before recording the comparison.
- [Risk] The full command family is broader than one immutable snapshot can
  safely serve → record shared versus process-bound projections explicitly;
  do not force unsupported sharing into the model.
- [Risk] Manual benchmark timing is noisy or unavailable on a contributor's
  machine → make deterministic counters/equivalence mandatory and represent
  unavailable resource metrics explicitly.
- [Risk] Checked-in evidence becomes stale after implementation or structural
  cleanup → mark it pre-implementation, record source/tool identity, and keep
  the harness as the refresh authority.
- [Risk] Evidence double-counts existing exact-request cache hits → separate
  cache-disabled/miss/hit modes and subtract cache-avoidable work from the
  prepared-state effect model.

## Migration Plan

No runtime migration is required. Add the harness and deterministic contract
tests, refresh the internal #493 evidence once on the selected release-build
environment, then use the recorded A/B/C outcome to decide whether #494 may
start. If the result is B or C, leave the prepared implementation lane
deferred and route evidence to the existing benchmark owners.
