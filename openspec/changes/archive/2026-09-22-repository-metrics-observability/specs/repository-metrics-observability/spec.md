## Purpose

Provide a deterministic, low-cost repository complexity snapshot that exposes size and dependency-structure evidence for reports, pull-request review, and neutral default-branch badges without becoming an architecture-governance score or gate.

## ADDED Requirements

### Requirement: Repository metrics are a typed informational snapshot

The system SHALL expose a versioned `repository-metrics/v1` snapshot with typed Size, Coupling, Structure, and per-project coupling values. The snapshot SHALL be additive to existing validation and health artifacts and SHALL NOT change Architecture Health, strict/audit outcomes, findings, exit codes, baseline debt, or metric-budget evaluation.

#### Scenario: Complete analysis produces stable metrics
- **WHEN** an ordinary analysis completes with a complete project/type/source fact set
- **THEN** the result contains a deterministic repository-metrics snapshot with size, coupling, and structure values
- **AND** repeating the same analysis inputs produces byte-equivalent metric JSON
- **AND** consumers that ignore the new field continue to read the existing report shape

#### Scenario: Metrics never become a governance verdict
- **WHEN** a metric increases, decreases, or cannot be calculated
- **THEN** the strict/audit result and Architecture Health remain determined solely by their existing authorities
- **AND** the metric state is reported as informational evidence rather than pass/fail quality

### Requirement: Size metrics use explicit source identity semantics

The snapshot SHALL report `sourceLines`, `sourceFiles`, `projects`, `types`, and `publicTypes`. `sourceLines` SHALL count physical lines in successfully read analyzed C# files after the existing generated-file exclusion and source ownership rules. A file participating in multiple compilation/source-root contexts SHALL count once by normalized repository-relative identity; a file that cannot be read or whose ownership is ambiguous SHALL not be silently counted as complete evidence.

#### Scenario: Overlapping source roots do not multiply files
- **WHEN** the same physical C# file is discovered through more than one configured source root
- **THEN** it contributes one source file and one physical line count to the repository snapshot

#### Scenario: Generated and unreadable files are explicit
- **WHEN** a generated C# file is encountered or an analyzed source file cannot be read
- **THEN** generated code is excluded according to the existing generated-file policy
- **AND** unreadable/ambiguous source evidence makes the source metric partial or unavailable rather than fabricating a complete value

### Requirement: Coupling metrics derive from the canonical project graph

The snapshot SHALL report unique directed project dependency edges, dependencies per project, dependency density, maximum fan-in and fan-out, and deterministic per-project Ca, Ce, and Instability values. Duplicate project-reference declarations SHALL count once. Dependency density SHALL be `edges / (projects * (projects - 1))` for more than one project and SHALL be zero for zero- or one-project graphs. Instability SHALL be `Ce / (Ca + Ce)` and SHALL be zero when both are zero.

#### Scenario: Duplicate project references are deduplicated
- **WHEN** a project graph contains repeated references to the same target project
- **THEN** the dependency count, fan-in, fan-out, Ca, and Ce count one directed edge

#### Scenario: Trivial project graphs have deterministic ratios
- **WHEN** the analyzed graph contains zero or one project, or an isolated project has no edges
- **THEN** density and instability are emitted as zero and no division-by-zero or unavailable ratio occurs

### Requirement: Structure metrics are linear graph projections

The snapshot SHALL report maximum dependency depth, non-trivial cyclic component count, cyclic project count and ratio, largest strongly connected component size and ratio. SCC and depth calculation SHALL operate on the canonical directed project graph in linear or near-linear time, SHALL count self-loops deterministically, and SHALL use a documented convention for empty and isolated graphs.

#### Scenario: Cycles are summarized without all-pairs analysis
- **WHEN** the project graph contains a cycle or self-loop
- **THEN** the cycle contributes to SCC metrics according to the deterministic SCC convention
- **AND** the calculation does not require an all-pairs path analysis

#### Scenario: Acyclic depth is deterministic
- **WHEN** the condensation graph is acyclic
- **THEN** maximum dependency depth is the longest directed edge count in that condensation graph, with zero for an empty or isolated graph

### Requirement: Partial and unavailable evidence is preserved

The snapshot SHALL carry an explicit availability state of complete, partial, or unavailable and stable reason codes. Missing base evidence, missing project discovery, blocked preflight, incomplete type/source facts, and incompatible metric schema SHALL never be represented as numeric zero unless the defined empty-graph or empty-source semantics apply.

#### Scenario: Blocked analysis does not fabricate metrics
- **WHEN** build-state preflight prevents authoritative analysis
- **THEN** the result marks repository metrics unavailable with a reason and omits trusted numeric values

#### Scenario: Missing base comparison is not zero
- **WHEN** a pull-request report has a current snapshot but no compatible base snapshot
- **THEN** the report marks the repository metrics delta unavailable and does not render base values as zero

### Requirement: Absolute reporting and badge projection are neutral

The human and machine-readable reporting surfaces SHALL expose the absolute current snapshot in a bounded grouped Size, Coupling, and Structure section. A Core/CLI-owned badge projection SHALL expose at least `Source lines` as a compact absolute Shields endpoint payload. Badge output SHALL use current verified main evidence only, SHALL contain no PR delta, threshold, quality color, or pass/fail interpretation, and SHALL preserve the existing trusted badge publication/privacy boundary.

#### Scenario: Absolute report is grouped and bounded
- **WHEN** a current repository metrics snapshot is complete or partial
- **THEN** human output groups the values under Size, Coupling, and Structure
- **AND** machine output contains typed stable fields without requiring formatted-text parsing

#### Scenario: Source lines badge is an absolute snapshot
- **WHEN** verified default-branch automation publishes a current metrics snapshot
- **THEN** the badge payload presents an absolute Source lines value
- **AND** it does not present a base-to-head delta or imply that magnitude is a governance result

### Requirement: Metrics calculation reuses existing analysis work

Repository metrics SHALL be calculated from the immutable analysis snapshot, already discovered source files, loaded types, and canonical project graph. The implementation SHALL not load MSBuild a second time, create a second semantic compilation, traverse the repository once per metric, or add a second publisher/comment path solely for metrics. Metrics calculation failures SHALL be isolated as partial/unavailable evidence.

#### Scenario: Normal analysis has one metrics projection
- **WHEN** an ordinary report requests repository metrics
- **THEN** the implementation derives all metric families from one retained snapshot/fact projection
- **AND** profiling evidence can show the metric projection did not introduce a second project load or semantic analysis pass
