# large-solution-benchmarking Specification

## Purpose
Provide one deterministic, privacy-safe benchmark foundation that downstream
performance investigations can reuse to vary large-solution work independently
and cite comparable analysis-profile evidence.

## Requirements

### Requirement: A deterministic workload corpus exposes independent scale dimensions

The benchmark foundation SHALL define a versioned, synthetic workload manifest
whose identity is derived from its canonical shape and dimension values. A
workload SHALL expose project, assembly, source, type, reference/edge,
selector/layer, contract, finding, and source-root dimensions independently
where the selected shape permits them. Recreating a workload with the same
manifest and generator version SHALL produce the same project topology, policy
inputs, source identities, and expected inventory counts.

Selector scale SHALL be named and counted as
`selector_predicate_terms_per_layer`: each term SHALL be materialized as a
distinct CEL predicate in every generated layer, and
`selector_predicate_evaluation_count` SHALL equal
`project_count × types_per_project × layer_count ×
selector_predicate_terms_per_layer`. Finding candidates SHALL select distinct
materialized synthetic types; a candidate SHALL not apply to every synthetic
type merely because its ID is different.

Contract scale SHALL be named `contracts_per_workload` and SHALL describe
the total generated contract count independently of `source_root_count`.
Changing source-root count alone SHALL not change contract count or generated
contract work. Derived deterministic counters SHALL use checked int32
arithmetic and SHALL fail closed when a declared combination exceeds the
representable counter range; the versioned JSON schemas SHALL declare matching
int32 upper bounds for dimensions and deterministic counters.

#### Scenario: Recreating a workload is deterministic

- **WHEN** two fixture roots are generated from the same manifest and generator version
- **THEN** their normalized manifests, project/reference topology, policy inputs, source inventory, and expected counts are identical

#### Scenario: A dimension can be varied without changing unrelated dimensions

- **WHEN** one supported dimension is changed while all other manifest values remain fixed
- **THEN** the generated evidence identifies that dimension as the only requested scale change and preserves the workload's shape and synthetic identity namespace

#### Scenario: Selector and finding dimensions describe materialized work

- **WHEN** selector predicate terms or finding candidates are increased
- **THEN** the generated policy contains the requested distinct selector terms
- **AND** each finding candidate targets one distinct synthetic type
- **AND** deterministic evidence reports the resulting selector evaluation and candidate counts rather than metadata-only IDs

#### Scenario: Source roots do not multiply contract work

- **WHEN** source-root count is increased while `contracts_per_workload` remains fixed
- **THEN** source files and source-root inventory may increase
- **AND** generated contract count and contract IDs remain unchanged

#### Scenario: Derived counter overflow fails closed

- **WHEN** a dimension combination would overflow a deterministic int32 counter
- **THEN** workload generation rejects the combination before emitting a manifest or evidence document

### Requirement: The corpus supports representative topology and consumer shapes

The corpus SHALL support linear/deep, wide fan-out/fan-in, diamond/alternate
path, dense, and cyclic/SCC graph shapes, plus many-project/few-type and
few-project/many-type shapes. It SHALL also provide reusable real-MSBuild
receipt-backed and prebuilt/staged-assembly shapes, the current full-governance
multi-command shape, and PR-shaped change dimensions. The real-MSBuild and
staged-assembly shapes SHALL remain distinguishable in evidence so preparation
cost is not attributed to the wrong adopter model.

#### Scenario: Graph topology shapes produce their declared structure

- **WHEN** a linear, wide, diamond, dense, or cyclic/SCC shape is materialized
- **THEN** its manifest records the requested topology, project/reference counts, and cycle/SCC disposition, and the generated project graph matches those declarations

#### Scenario: Cyclic topology fails closed at the compilation boundary

- **WHEN** a `CyclicScc` workload is passed to the v1 fixture materializer
- **THEN** materialization rejects project-based compilation with an explicit structural-only limitation
- **AND** the workload remains available for deterministic graph/topology evidence without claiming executable MSBuild or staged-assembly coverage
- **AND** an external IL/assembly producer is required before a future version may provide an executable cyclic staged benchmark

#### Scenario: Consumer shapes preserve their preparation boundary

- **WHEN** a real-MSBuild or prebuilt/staged-assembly workload is selected
- **THEN** the fixture records whether candidate compilation is owned by the fixture's MSBuild build or supplied as externally staged analysis evidence, without changing the ArchLinterNet analysis policy semantics

### Requirement: Benchmark evidence composes analysis-profile/v1 without weakening it

The foundation SHALL provide a `benchmark-evidence/v1` machine-readable
document that retains the raw `analysis-profile/v1` profile for every measured
run and adds workload identity, independent dimensions, project/assembly/source
/type/reference/edge counts, selector/layer and contract/finding counts where
available, canonical-result identity, cache/prepared mode, environment and
resource availability, and measurement provenance. Deterministic counters and
environment-dependent measurements SHALL be distinguishable. The benchmark
evidence document SHALL not change finding identity, ordering, exit semantics,
or the `analysis-profile/v1` schema.

#### Scenario: Evidence retains raw profile and workload identity

- **WHEN** a measured run is recorded
- **THEN** the document contains the exact workload manifest identity, mode/configuration, raw analysis-profile/v1 payload, canonical-result identity, and provenance needed to reproduce the run

#### Scenario: Unavailable resource measurements are explicit

- **WHEN** a platform cannot provide a requested peak-memory, allocation, CPU,
  cache, or prepared-state measurement
- **THEN** the evidence records an explicit unavailable status or null value with a reason and does not substitute zero or a fabricated estimate

### Requirement: Complexity evidence is measurement-first and multi-size

The foundation SHALL represent independent scale variables, deterministic work
counters, observed normalized growth, and current/target work models for a
material hotspot. A complexity claim SHALL cite code-path/work-counter evidence
and measurements at at least three useful sizes when technically practical;
wall-clock timings alone SHALL NOT establish a complexity claim. The evidence
format SHALL support recording that a suspected hotspot was not reproduced.

#### Scenario: A material hypothesis records its work model

- **WHEN** a benchmark result is used to evaluate a material hot path
- **THEN** the evidence records the scale variable, at least three size points when practical, deterministic work counters, current model, observed growth, and target model or an explicit non-reproduction disposition

#### Scenario: Timing noise does not become a deterministic gate

- **WHEN** benchmark samples are compared across environments
- **THEN** deterministic counters and canonical results remain regression evidence while wall-clock, CPU, allocation, and peak-memory values are labeled environment-dependent observations

### Requirement: Optimization candidates carry an expected-effect contract before implementation

The benchmark evidence SHALL support a pre-implementation expected-effect
record containing the targeted phase share, current and target work models,
local effect estimates for representative sizes, an end-to-end bound where
applicable, memory/allocation/storage/I/O trade-offs, cold versus warm or cache
/prepared modes, an issue-specific success threshold and kill criterion, and
confidence or uncertainty. The foundation SHALL not authorize an optimization
solely because a benchmark is slow.

#### Scenario: An optimization disposition is measurable before coding

- **WHEN** an issue proposes an optimization from benchmark evidence
- **THEN** its evidence can record the baseline share, expected local and end-to-end effect, trade-offs, success threshold, kill criterion, and uncertainty before implementation begins

### Requirement: Canonical equivalence and reusable workflow modes are recorded

The benchmark foundation SHALL support cold, repeated warm, cache-disabled,
cache-miss/population, verified-hit, prepared/unprepared, sequential, and
bounded-parallel modes when applicable. It SHALL record canonical-result
equivalence across compared modes and support the current v0.8 full-governance
command family mix and #493's multi-command workload without creating a
performance-only semantics. Benchmark harnesses SHALL remain explicitly
invoked and excluded from the default correctness test gate.

#### Scenario: Compared modes retain canonical equivalence

- **WHEN** two applicable benchmark modes analyze the same authoritative workload
- **THEN** their evidence records matching canonical findings/result identity and ordering, or records an explicit failed equivalence check

#### Scenario: The default test gate does not run hardware-sensitive matrices

- **WHEN** the repository's normal test or acceptance target runs
- **THEN** it exercises deterministic corpus/evidence contract tests but does not execute the explicit hardware-sensitive benchmark matrix or rewrite checked-in measurements

### Requirement: Public benchmark evidence is synthetic and downstream-reusable

Committed manifests, fixtures, schemas, and evidence SHALL contain only
synthetic/anonymized identities and SHALL exclude private adopter names,
repositories, namespaces, proprietary topology, URLs, and raw private CI logs.
The corpus SHALL expose a stable reuse contract so #503, #655, #675, #493,
and future v0.9 measurement lanes can extend or select workloads without
creating competing scale frameworks.

#### Scenario: Privacy review rejects private provenance

- **WHEN** committed benchmark artifacts are inspected
- **THEN** their identities, paths, namespaces, and provenance are synthetic or tool-local and contain no private adopter identity or raw private log

#### Scenario: A downstream lane selects an existing workload

- **WHEN** a downstream measurement lane requests a supported shape and dimension set
- **THEN** it can consume the canonical manifest/generator and evidence contract without duplicating the corpus or changing product semantics
