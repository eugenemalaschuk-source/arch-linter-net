## ADDED Requirements

### Requirement: A validated, bounded max-parallelism option is exposed by CLI and Testing API
The system SHALL provide a `--max-parallelism <n>` option on the CLI `validate` command and an `ArchitectureValidationBuilder.WithMaxParallelism(int? maxDegreeOfParallelism)` method on the Testing API, both resolving to the same effective per-request degree of parallelism used for bounded parallel scanning. An unset value SHALL resolve to `max(1, min(Environment.ProcessorCount, 4))`. A supplied value `<= 0` SHALL be rejected as an invalid input (a CLI runtime error / a thrown Testing API exception) before any scanning begins, never silently clamped to a valid value.

#### Scenario: Omitting the option uses the conservative default
- **WHEN** `validate` runs without `--max-parallelism` (or `WithMaxParallelism` is not called)
- **THEN** the effective max parallelism equals `max(1, min(Environment.ProcessorCount, 4))`

#### Scenario: An explicit positive override is honored
- **WHEN** `--max-parallelism 8` (or `WithMaxParallelism(8)`) is supplied
- **THEN** the effective max parallelism equals `8`, regardless of the default formula

#### Scenario: Zero or negative values are rejected before scanning begins
- **WHEN** `--max-parallelism 0` or `--max-parallelism -1` (or the equivalent `WithMaxParallelism` value) is supplied
- **THEN** the request is rejected as invalid input and no project discovery, assembly resolution, or scanning occurs

### Requirement: max-parallelism 1 is a first-class supported sequential mode
The system SHALL treat `--max-parallelism 1` / `WithMaxParallelism(1)` as running every scanning phase on the calling thread with no `Parallel`/`Task`-based scheduling overhead, functionally and behaviorally equivalent to the scanning implementation that existed before bounded parallel scanning was introduced.

#### Scenario: max-parallelism 1 produces identical output to the parallel default
- **WHEN** the same policy and target assemblies are validated once with `--max-parallelism 1` and once with the default resolved parallelism
- **THEN** the produced findings, finding identity, ordering, and exit status are identical between the two runs

### Requirement: Type loading is parallelized without changing output order or content
The system SHALL partition `ArchitectureTypeIndex.LoadAllTypes()`'s per-target-assembly type loading across up to the effective max-parallelism degree of concurrent workers, computing each assembly's loadable-type set independently, and SHALL merge the partitioned results back into the exact original assembly-iteration order before exposing the combined `Type[]` — never assembly-completion order.

#### Scenario: Parallel type loading matches sequential output at every supported degree
- **WHEN** the same target assemblies are loaded once with `--max-parallelism 1` and once with `--max-parallelism 4`
- **THEN** `ArchitectureTypeIndex`'s resulting type collection is identical in both content and order between the two runs

#### Scenario: Assembly-completion order never leaks into output order
- **WHEN** target assemblies are loaded in parallel and a later-indexed assembly's type enumeration completes before an earlier-indexed assembly's
- **THEN** the merged type collection still reflects the original assembly declaration/resolution order, not completion order

### Requirement: Source-file fact index materialization is parallelized without changing output order or content
The system SHALL partition `ArchitectureSourceFileFactIndex.BuildData()`'s reflection pass (by pre-sorted assembly) and source-file enumeration pass (by source root) across up to the effective max-parallelism degree of concurrent workers, and SHALL merge each partition's results back in the same pre-established deterministic order the sequential implementation already establishes (sorted-assembly order for the reflection pass, source-root declaration order for the source scan) before the existing final sort of facts and ambiguities runs.

#### Scenario: Parallel fact-index materialization matches sequential output at every supported degree
- **WHEN** the same target assemblies and source roots are indexed once with `--max-parallelism 1` and once with `--max-parallelism 4`
- **THEN** the resulting declared-type facts and source ambiguities are identical in both content and order between the two runs

#### Scenario: A repeated run with the same immutable inputs is byte-stable
- **WHEN** the same fact-index materialization is run twice with the same parallelism degree greater than one
- **THEN** the serialized fact/ambiguity output is byte-identical between the two runs

### Requirement: No target assembly or fact index is scanned or materialized more than once per snapshot
The system SHALL preserve the existing once-per-snapshot laziness guarantee for `ArchitectureTypeIndex`/`ArchitectureSourceFileFactIndex` when bounded parallel scanning is used: introducing parallel partitions within a single materialization pass SHALL NOT cause that pass, or any individual assembly/source-root partition within it, to execute more than once for the snapshot's lifetime.

#### Scenario: Repeated access after parallel materialization does not rematerialize
- **WHEN** `ArchitectureTypeIndex`/`ArchitectureSourceFileFactIndex` is accessed more than once after bounded parallel materialization has already completed
- **THEN** no additional assembly loading, reflection, or source-file scanning occurs

### Requirement: Small work sets and sequential mode skip parallel scheduling overhead
The system SHALL run the existing sequential loop directly, without allocating `Parallel`/`Task`-based scheduling, whenever a scanning phase's partition count is below a fixed small threshold or the effective max parallelism is `1`.

#### Scenario: A small target-assembly set avoids parallel setup overhead
- **WHEN** a snapshot's target assembly set has a partition count below the fixed threshold
- **THEN** `ArchitectureTypeIndex.LoadAllTypes()` executes its existing sequential loop and does not invoke bounded-parallel scheduling

### Requirement: Bounded parallel scanning is cancellation-safe
The system SHALL observe the session's `CancellationToken` from within each concurrent partition worker and SHALL discard any completed or in-flight partition results — publishing nothing — when cancellation is observed before that phase's deterministic merge step completes. A caught `AggregateException` wrapping cancellation SHALL be unwrapped and re-raised as `OperationCanceledException` directly, never left wrapped.

#### Scenario: Cancellation during parallel type loading publishes no partial result
- **WHEN** the token is cancelled while `ArchitectureTypeIndex.LoadAllTypes()`'s parallel partitions are still executing
- **THEN** `LoadAllTypes()` raises `OperationCanceledException` directly (not wrapped in `AggregateException`) and no partially merged type collection is exposed

#### Scenario: Cancellation during parallel fact-index materialization publishes no partial result
- **WHEN** the token is cancelled while `ArchitectureSourceFileFactIndex.BuildData()`'s parallel partitions are still executing
- **THEN** `BuildData()` raises `OperationCanceledException` directly and no partially merged fact/ambiguity collection is exposed

### Requirement: Cached and uncached execution remain equivalent under bounded parallel scanning
The system SHALL NOT change the existing per-mode cache-lookup-then-scan ordering: a cache hit for a requested mode SHALL continue to skip the mode's contract execution (and therefore never trigger materialization of the parallelized indexes on that mode's behalf), while a miss SHALL fall through to the unchanged full pipeline, now potentially using bounded parallel scanning for type loading and fact-index materialization.

#### Scenario: A cache hit never triggers parallel scanning
- **WHEN** every requested mode's cache lookup reports `Hit`
- **THEN** neither `ArchitectureTypeIndex.LoadAllTypes()` nor `ArchitectureSourceFileFactIndex.BuildData()` executes for that snapshot

#### Scenario: A cache miss uses bounded parallel scanning identically to a cache-disabled run
- **WHEN** the same policy and target assemblies are validated once with the cache disabled and once with the cache enabled but missing
- **THEN** the resulting findings, finding identity, and ordering are identical between the two runs at the same effective max parallelism
