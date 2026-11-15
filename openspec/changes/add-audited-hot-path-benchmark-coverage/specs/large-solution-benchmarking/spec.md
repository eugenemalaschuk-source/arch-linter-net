# Spec Delta

## MODIFIED Requirements

### Requirement: A deterministic workload corpus exposes independent scale dimensions

The benchmark foundation SHALL define a versioned, synthetic workload manifest whose identity is derived from its canonical shape and dimension values. A workload SHALL expose project, assembly, source-file, type, reference/edge, selector/layer, contract, ignore, finding, metric-rule, report-rule, and source-root dimensions independently where the selected shape permits them. Recreating a workload with the same manifest and generator version SHALL produce the same project and contract-graph topology, policy inputs, source identities, and expected inventory counts.

Selector scale SHALL be named and counted as `selector_predicate_terms_per_layer`: each term SHALL be materialized as a distinct CEL predicate in every generated layer, and `selector_predicate_evaluation_count` SHALL equal `project_count × types_per_project × layer_count × selector_predicate_terms_per_layer`. Finding candidates SHALL select distinct materialized synthetic types; a candidate SHALL not apply to every synthetic type merely because its ID is different.

Contract scale SHALL be named `contracts_per_workload` and SHALL describe the total generated contract count independently of `source_root_count`. Ignore, metric-rule, report-rule, finding, source-file, and project scale SHALL be recorded as separate dimensions. Changing one supported dimension alone SHALL preserve unrelated generated inventory and policy counts. Derived deterministic counters SHALL use checked int32 arithmetic and SHALL fail closed when a declared combination exceeds the representable counter range; the versioned JSON schemas SHALL declare matching int32 upper bounds for dimensions and deterministic counters.

#### Scenario: Recreating a workload is deterministic

- **WHEN** two fixture roots are generated from the same manifest and generator version
- **THEN** their normalized manifests, project and contract-graph topology, policy inputs, source inventory, and expected counts are identical

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

#### Scenario: Ignore and rule dimensions are independent

- **WHEN** ignore entries, metric rules, or report rules are varied independently from contracts, findings, source files, and projects
- **THEN** only the selected dimension and its derived inventory change
- **AND** the generated policy or report input contains exactly the declared entries

#### Scenario: Derived counter overflow fails closed

- **WHEN** a dimension combination would overflow a deterministic int32 counter
- **THEN** workload generation rejects the combination before emitting a manifest or evidence document

### Requirement: The corpus supports representative topology and consumer shapes

The corpus SHALL support linear/deep, wide fan-out/fan-in, diamond/alternate-path, dense, and cyclic/SCC project graph shapes, plus many-project/few-type and few-project/many-type shapes. It SHALL also provide executable layer/type-reference graphs for dense DAGs, layered DAGs, and SCCs with acyclic tails, and each applicable cycle workload SHALL execute both cycle and acyclic-sibling contract families against compiled synthetic inputs. The real-MSBuild and staged-assembly shapes SHALL remain distinguishable in evidence so preparation cost is not attributed to the wrong adopter model.

#### Scenario: Graph topology shapes produce their declared structure

- **WHEN** a linear, wide, diamond, dense, or cyclic/SCC project shape is declared
- **THEN** its manifest records the requested topology, project/reference counts, and cycle/SCC disposition
- **AND** the generated project graph matches those declarations

#### Scenario: Cyclic topology fails closed at the compilation boundary

- **WHEN** a `CyclicScc` workload is passed to the v1 fixture materializer
- **THEN** materialization rejects project-based compilation with an explicit structural-only limitation
- **AND** the workload remains available for deterministic graph/topology evidence without claiming executable MSBuild or staged-assembly coverage
- **AND** an external IL/assembly producer is required before a future version may provide an executable cyclic staged benchmark

#### Scenario: Cyclic project topology remains structural-only

- **WHEN** a `CyclicScc` project workload is passed to the v1 project fixture materializer
- **THEN** materialization rejects project-reference compilation with an explicit structural-only limitation
- **AND** the workload remains available for deterministic project-graph topology evidence without claiming executable MSBuild or staged-assembly coverage

#### Scenario: Layer graph cycles are executable without project-reference cycles

- **WHEN** a dense DAG, layered DAG, or SCC-with-acyclic-tail layer graph is materialized
- **THEN** its type references are compiled within a buildable project graph
- **AND** the generated policy invokes both the cycle and acyclic-sibling contract families
- **AND** the benchmark asserts the declared empty or non-empty cycle output and canonical result

#### Scenario: Consumer shapes preserve their preparation boundary

- **WHEN** a real-MSBuild or prebuilt/staged-assembly workload is selected
- **THEN** the fixture records whether candidate compilation is owned by the fixture's MSBuild build or supplied as externally staged analysis evidence
- **AND** both shapes use the same ArchLinterNet policy semantics

### Requirement: Benchmark evidence composes analysis-profile/v1 without weakening it

The foundation SHALL provide a `benchmark-evidence/v1` document that retains the raw `analysis-profile/v1` profile for every measured run and adds workload and materialized-input identities, independent dimensions, bounded deterministic work counters, canonical-result identity, exact source/runtime/runner/tool provenance, measurement boundaries, profiler-overhead evidence, and resource availability. Deterministic counters and environment-dependent measurements SHALL be distinguishable. The benchmark evidence document SHALL not change finding identity, ordering, exit semantics, cache authorization, or the `analysis-profile/v1` schema.

#### Scenario: Evidence retains raw profile and workload identity

- **WHEN** a measured run is recorded
- **THEN** the document contains the exact workload manifest and materialized-input identities, mode/configuration, raw analysis-profile/v1 payload, canonical-result identity, and provenance needed to reproduce the run

#### Scenario: Work observations are bounded and separate from the profile schema

- **WHEN** benchmark work is observed for graph, membership, parsing, build-context, method-body, CEL, AST, or History paths
- **THEN** the evidence uses a fixed low-cardinality counter vocabulary without file/type labels
- **AND** those observations do not add fields to or change the semantics of `analysis-profile/v1`

#### Scenario: Measurement boundaries and profiler overhead are attributable

- **WHEN** benchmark samples are compared
- **THEN** fixture restore/build, process startup, analyzer profile phases, report publication, and profiler overhead are separately identified where measured
- **AND** a measured process wall-clock value is not presented as analyzer-only time

#### Scenario: Unavailable resource measurements are explicit

- **WHEN** a platform cannot provide a requested CPU, allocation, peak-memory, or profiler-overhead measurement
- **THEN** the evidence records an explicit unavailable status or null value with a reason and does not substitute zero or a fabricated estimate

#### Scenario: Cache outcomes remain fail-closed

- **WHEN** a cache request is ineligible or its population evidence is pending
- **THEN** the benchmark SHALL NOT record a verified hit
- **AND** build, restore, and process-start time remain separate from analyzer time

### Requirement: Complexity evidence is measurement-first and multi-size

The foundation SHALL represent independent scale variables, deterministic work counters, observed normalized growth, and current/target work models for a material hotspot. A complexity claim SHALL cite code-path/work-counter evidence and repeated measurements at at least three useful sizes when technically practical; wall-clock timings alone SHALL NOT establish a complexity claim. The evidence format SHALL support explicit not-measured values and a suspected hotspot not reproduced disposition.

#### Scenario: A material hypothesis records its work model

- **WHEN** benchmark results are used to evaluate a material hot path
- **THEN** the evidence records the scale variable, at least three size points when practical, deterministic work counters, current model, observed growth, and target model or an explicit non-reproduction disposition
- **AND** it records the exact input, source revision, runtime, runner, timer boundary, profiler overhead, resource measurements, and canonical-result checksum

#### Scenario: Timing noise does not become a deterministic gate

- **WHEN** benchmark samples are compared across environments
- **THEN** deterministic counters and canonical results remain regression evidence while wall-clock, CPU, allocation, peak-memory, and profiler-overhead values are labeled environment-dependent observations

#### Scenario: An unmeasured value stays explicit

- **WHEN** a phase share, allocation, retained-memory value, or expected effect has not been measured
- **THEN** the evidence marks that value as not measured
- **AND** it does not infer a current .NET result from source-level models or historical operation-count reproductions

### Requirement: History workloads are reusable by algorithm-owning tasks

The benchmark foundation SHALL provide synthetic, deterministic History inputs for the algorithm families owned by downstream tasks, with independent scale variables, required-output cardinality, bounded work counters, and canonical-result checks. History measurement SHALL extend the existing #502 catalog and evidence format rather than create a competing benchmark framework.

#### Scenario: A History task selects a supported workload

- **WHEN** a downstream History task measures sparse line edits, task-to-commit occurrences, co-change components, or ancestry queries
- **THEN** it can select at least three appropriate sizes from the shared workload catalog
- **AND** its evidence distinguishes algorithm work from required output materialization

#### Scenario: History results remain reproducible

- **WHEN** the same History workload and generator version are executed again
- **THEN** its synthetic inputs, deterministic work counts, and canonical output identity are unchanged

### Requirement: Historical coverage corrections preserve previous evidence

The benchmark evidence SHALL retain historical measurements and dispositions when a later coverage review finds that a workload did not execute the path its interpretation appeared to cover. A correction SHALL be dated, identify the uncovered execution boundary, and keep new measurements separate from historical samples.

#### Scenario: A historical graph result is narrowed after coverage review

- **WHEN** a prior graph matrix measured project topology without invoking cycle or acyclic-sibling contracts
- **THEN** its recorded measurements and disposition remain unchanged
- **AND** a dated coverage note states that the matrix does not establish cycle-checker execution, cycle output, or cycle-phase attribution

#### Scenario: A new baseline is not substituted for an old one

- **WHEN** an executable cycle workload is measured on a later source revision
- **THEN** the new revision, workload identity, samples, and canonical outputs are recorded separately from the historical graph matrix
