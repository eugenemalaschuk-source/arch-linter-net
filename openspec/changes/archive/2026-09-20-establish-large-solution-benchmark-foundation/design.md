## Context

The repository already owns `analysis-profile/v1`, an adoption-acceptance
corpus, and explicit #374/#409 profile harnesses. Those assets are authoritative
for profile semantics and current v0.8 governance behavior, but the existing
large fixture is fixed and the harnesses do not model independent graph and
scale dimensions. See `proposal.md` and the new delta specification for the
behavioral contract.

## Goals / Non-Goals

**Goals:**

- Make workload shape and scale explicit, deterministic, and reusable.
- Keep fixture generation test-local and privacy-safe while still exercising
  real project discovery, MSBuild, staged-assembly, and CLI paths.
- Preserve raw `analysis-profile/v1` payloads and separate deterministic work
  evidence from environment-dependent measurements.
- Give downstream issues a single manifest/evidence contract for comparison.

**Non-Goals:**

- No production algorithm, cache, prepared-state, incremental, or concurrency
  optimization.
- No new CLI command, package dependency, or public library API.
- No universal timing SLA, profiler replacement, hosted performance service, or
  release authorization.

## Decisions

### Keep the corpus in Core.Tests and generate temporary roots

The generator and materializer live in `tests/ArchLinterNet.Core.Tests` beside
the existing adoption fixture. It writes deterministic temporary roots for
integration runs and keeps only small manifests/schema/docs in the repository.
This reuses the existing test project's real CLI/MSBuild boundary without
shipping benchmark code in a product package.

Alternatives considered: putting a new benchmark product in `src/` would expose
an unnecessary API and package surface; adding every scale point as checked-in
projects would make the repository large and encourage fixture drift.

### Model topology before materialization

The generator first creates an immutable workload description containing shape,
dimensions, project nodes, edges, source/type counts, policy declarations, and
expected inventory. Separate materializers render ordinary project files,
solution files, policies, staged assemblies, and PR/change metadata. Canonical
ordering is ordinal and all generated names are derived from the workload seed.

This separation makes dimensions independently testable and lets downstream
lanes consume graph/count evidence without starting a process.

### Use explicit named shapes, not a free-form graph DSL

Named shapes cover the issue's required topologies and avoid ambiguous graph
semantics. Each shape validates the dimensions it supports and reports an
argument error for impossible combinations (for example a diamond with fewer
than four nodes). A shared deterministic graph model records edges once and
derives fan-in/fan-out, cycle/SCC, and alternate-path facts from the same data.

### Treat real-MSBuild and staged assemblies as distinct materializers

The real-MSBuild fixture renders buildable `csproj`/`slnx` projects and uses the
existing build-state/receipt path. The staged-assembly fixture renders a
candidate project plus externally supplied assembly evidence and explicitly
marks compilation as external. Both use the same policy/workload identity and
canonical result projection so #675 and #493 can compare preparation without
creating a second scale framework.

### Wrap, do not mutate, analysis-profile/v1

`benchmark-evidence/v1` stores raw profile JSON as an embedded payload and adds
benchmark-specific metadata in a separate envelope. A typed model validates
required deterministic fields and represents unavailable resource values
explicitly. This avoids changing the reviewed Core profile API/snapshot while
allowing workload counts, complexity models, expected-effect estimates, and
downstream dispositions to evolve under a new evidence version.

### Canonical result identity is normalized before hashing

Result identity is computed from a stable, sorted canonical projection supplied
by the harness (findings/diagnostics plus status/order evidence), not from raw
human output or paths under temporary roots. The generator also exposes a
workload/input identity. A mismatch is recorded as a failed equivalence check;
it is never hidden by comparing only counts.

### Keep measurement execution explicit

Deterministic unit tests cover generation, manifest validation, topology, and
evidence round trips. Hardware-sensitive multi-size/process matrices are
explicit NUnit fixtures and are not selected by `make test`/`make acceptance`.
Checked-in evidence remains a manually refreshed observation with environment,
source, binary, and package identity where a run exists.

## Risks / Trade-offs

- [Risk] Generated real-MSBuild projects can make focused tests slow or flaky →
  keep normal tests at manifest/model level, bound generated fixture sizes, and
  retain explicit integration/benchmark selection.
- [Risk] A synthetic graph may not exercise every analyzer path → record shape
  and coverage limits in the manifest and allow downstream lanes to extend the
  same generator rather than claiming universal representativeness.
- [Risk] Raw JSON profile structure can drift → validate `SchemaId` and required
  profile payload presence, reuse the existing profile schema tests, and keep
  evidence versioned independently.
- [Risk] Temporary paths can leak into checksums → normalize paths at the
  canonical projection boundary and test two independently materialized roots.
- [Risk] Benchmark users may mistake timing for an SLA → label every
  environment-dependent measurement and state non-universality in the schema and
  internal documentation.

## Migration Plan

No runtime migration is required. Existing #374/#409 harnesses continue to own
their checked-in historical evidence. New benchmark lanes select the canonical
corpus and may gradually replace duplicated local fixture definitions; no
existing public profile or adoption fixture is removed in this change.

## Open Questions

None. The exact downstream workload sizes remain measurement decisions owned by
each consuming issue and do not change this contract.
