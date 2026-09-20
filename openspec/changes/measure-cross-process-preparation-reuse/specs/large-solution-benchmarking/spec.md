## ADDED Requirements

### Requirement: The canonical corpus exposes a current multi-command adopter workflow

The benchmark foundation SHALL provide a current full-governance workload that
can exercise strict validation, audit validation, no-new-debt, Architecture
Health, current-side change snapshots, and applicable topology, measure,
baseline, and public-API projections without requiring an arbitrary command
that the representative workflow does not invoke. It SHALL provide
distinguishable real-MSBuild receipt-backed and prebuilt/staged-assembly
preparation boundaries, and every measured process SHALL retain that boundary
in its evidence.

#### Scenario: Consumer shapes preserve their preparation boundary

- **WHEN** a real-MSBuild or prebuilt/staged-assembly workload is selected
- **THEN** the fixture records whether candidate compilation is owned by the
  fixture's MSBuild build or supplied as externally staged analysis evidence
- **AND** both shapes use the same ArchLinterNet policy semantics.

#### Scenario: Current governance commands are represented explicitly

- **WHEN** a full-governance workload is measured
- **THEN** its manifest identifies each exercised command family and process
  boundary
- **AND** optional baseline verification, public-API comparison, topology,
  and measure work is included only when the declared workflow invokes it
- **AND** the command description does not claim an unmeasured projection.

### Requirement: Cross-process preparation reuse is decided against a simpler in-process alternative

The benchmark SHALL compare, for the same representative current-governance
workload, independent one-shot processes, one-process multi-projection execution
over one immutable analysis snapshot/session, and a persisted prepared-state
expected-effect model. It SHALL quantify repeated preparation/fact work per
process, safe shared projections, projections requiring process/lifetime
separation, and deterministic/canonical equivalence of the compared modes. The
one-process alternative SHALL be evaluated before a persisted-state model can
receive outcome A authorization.

#### Scenario: One-process sharing is measured before persistence

- **WHEN** a candidate workload has multiple read-only command projections
- **THEN** the evidence records the projections served by one immutable snapshot,
  the preparation/fact work performed once, and the projections that remain
  process-bound
- **AND** the evidence compares its work and resource cost with independent
  one-shot execution over the same candidate state.

#### Scenario: Persisted reuse is not authorized by process count alone

- **WHEN** repeated work exists across independent consumers
- **THEN** the decision records whether persisted reuse has material distinct
  value beyond the one-process alternative
- **AND** outcome A is permitted only with a measured effect model and
  break-even point
- **AND** outcomes B or C record the reason to defer or route the opportunity.

### Requirement: The prepared-analysis expected-effect contract is recorded before implementation

For outcome A, the evidence SHALL record the representative independent process
count and mix `R`, repeated-work share `p`, deterministic counts for the
candidate prepared boundary, cold prepare cost, per-consumer load/authorization
cost, crossover point, representative small/medium/large expected effect,
whole-workflow upper bound, storage/I/O/allocation/memory trade-offs, interaction
with exact-request `analysis-cache/v1` hits, and issue-specific success and kill
criteria. The contract SHALL be marked as pre-implementation evidence and
SHALL not claim an actual prepared implementation result.

#### Scenario: Break-even is derived from measured work

- **WHEN** outcome A is selected
- **THEN** the evidence contains enough measurements to compare
  `R × one-shot preparation` with `prepare + R × load/authorization`
- **AND** it records the first representative `R` where the persisted model is
  cheaper, or explicitly records that no crossover was observed.

#### Scenario: Cache hits are not double-counted

- **WHEN** the workflow includes analysis-cache/v1 disabled, miss, or hit modes
- **THEN** the effect model identifies cache-avoidable work separately from
  prepared-state-avoidable work
- **AND** an exact-request cache hit is not counted as a benefit of persisted
  prepared analysis.

### Requirement: Multi-command evidence preserves process and revision attribution

Multi-command `benchmark-evidence/v1` SHALL identify the command family,
process ordinal, candidate/base revision role, and whether the run is an
independent process or an in-process projection. Base-revision analysis SHALL
have separate attribution/cache routing and SHALL not count as reusable
candidate-state preparation. Equivalent real-MSBuild and staged-assembly
candidate projections SHALL retain canonical findings, identity, ordering, and
exit semantics, or the decision SHALL fail closed.

#### Scenario: Reference analysis is not counted as candidate reuse

- **WHEN** a workflow measures both a base revision and the unchanged candidate
  revision
- **THEN** the base sample is recorded as a separate revision role
- **AND** its preparation/fact counters are excluded from candidate reuse
  savings and persisted-state break-even calculations.

#### Scenario: The default test gate does not run the decision matrix

- **WHEN** the repository's normal test or acceptance target runs
- **THEN** it exercises deterministic workload/evidence contract tests
- **AND** it does not execute the explicit multi-process matrix, rewrite
  checked-in measurements, or authorize a prepared-state implementation.
