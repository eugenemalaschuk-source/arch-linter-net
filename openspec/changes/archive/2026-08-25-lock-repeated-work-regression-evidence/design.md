## Context

The existing `ArchitectureSessionMetadataIndexesTests` and
`PublicApiSurfaceMaterializationTests` prove #652 and #653 independently.
Their direct session seams are the correct deterministic evidence point:
internal counters do not belong in the versioned `analysis-profile/v1` schema.
See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**

- Combine all four metadata-family paths and repeated public-API evaluation in
  one small, anonymized fixture while isolating each materialization path in a
  fresh immutable session.
- Make the fan-out explicit (24 projects and 16 repeated contract checks), so
  a return to per-contract reconstruction fails deterministically.
- Lock a non-empty consumer-visible result projection with a checked-in
  checksum, plus actual Testing API outcomes and CLI exit semantics,
  independently of unstable timing or allocation measurements.

**Non-Goals:**

- New runtime indexes, profiling fields, policy syntax, CLI options, public
  APIs, persisted state, or a large-solution benchmark matrix.
- Hardware-sensitive duration or allocation thresholds.
- Work on additional hot paths; any such observation is deferred to #19/#461.

## Decisions

### 1. Use an in-memory session fixture rather than a new on-disk solution

The fixture will construct discovered project metadata and an
`ArchitectureAnalysisSession` directly, matching the tests that introduced
the covered indexes. It will use a real already-loaded test assembly for the
two public-API contracts. This keeps the fixture synthetic, small, and
deterministic while still exercising the consumer shape that causes contract
fan-out.

**Alternative considered:** extend the manual `analysis-profile/v1` benchmark
harness. Rejected because its process timing and profile outputs intentionally
do not expose the internal counters needed for a release-blocking regression
assertion, and it would duplicate #502's broad benchmark scope.

### 2. Assert each family's transition from untouched to reused

Each package-dependency, framework-reference, assembly-dependency,
project-metadata, and public-API fan-out will run in its own fresh session.
The fixture will assert the relevant counter is zero before that family's first
contract, one immediately after it, and still one after the remaining fan-out.
No other family may seed the counter. This detects a path that entirely bypasses
the session projection as well as a return to repeated session materialization.

### 3. Use a checked-in canonical checksum and host-level outcome assertions

The failing consumer-shaped fixture will produce an ordered, non-empty
canonical projection whose SHA-256 checksum and finding count are literal test
constants. The test must not derive an expected result from another execution
of the current implementation. Before accepting those literals, the same
document shape and projection calculation will run against detached baseline
`ef78023f420a6b2670b0c4fc6ad426df799c0dc4`, before #653/#652; its strict and
audit count/checksum output must exactly match the literals. The internal
evidence document will record the baseline revision and both values. A second
temporary-policy scenario will assert the actual `ArchitectureValidationBuilder`
strict/audit outcomes and the CLI's corresponding exit codes.

**Alternative considered:** compare two optimized sessions. Rejected because
that proves only determinism and permits a stable behavior regression.

**Alternative considered:** write a wall-clock or allocation assertion.
Rejected because those are useful only as optional, environment-sensitive
observations and are explicitly not a release contract for #654.

### 4. Record the evidence boundary in internal documentation

The internal analysis-profile dictionary will point readers to the focused
deterministic fixture and explain that it complements, rather than expands,
the existing manually-run `analysis-profile/v1` harnesses.

### 5. Route real host execution to the E2E bucket

The temporary-policy assertion creates a project on disk and launches the CLI
as a child process. It therefore belongs in the ordinary E2E bucket, even
though the other assertions in its fixture are in-memory Core checks. The
fixture will be listed in both the positive E2E filter and the explicit unit
exclusions, and carry NUnit's human-readable `E2E` category. The process
helper will enforce a bounded timeout, cancel its redirected stream reads, and
kill the entire process tree on timeout so a stalled child cannot poison later
test work.

## Risks / Trade-offs

- **[Risk]** Direct session construction could omit a consumer-visible result
  surface or capture a post-optimization-only golden. **Mitigation:** use a
  literal canonical checksum for its complete non-empty projection, record its
  independent pre-#653/#652 baseline provenance, and assert Testing API/CLI
  outcomes.
- **[Risk]** An aggregate counter masks a family bypass. **Mitigation:** use a
  fresh session and an explicit `0 -> 1 -> 1` transition for every family.
- **[Risk]** The fixture becomes a second benchmark framework. **Mitigation:**
  keep one fixed fan-out shape, no timing loop, generated artifacts, or
  performance baselines.
- **[Risk]** A new unrelated hot path appears while implementing it.
  **Mitigation:** record no code expansion; route it to #19/#461 as required
  by the issue.

## Migration Plan

No migration is needed. The change is tests and internal documentation only;
rollback is a normal revert with no persisted or public compatibility impact.
