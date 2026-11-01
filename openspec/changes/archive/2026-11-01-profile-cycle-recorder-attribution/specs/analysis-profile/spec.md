## ADDED Requirements

### Requirement: Opt-in profiles attribute cycle baseline recorder workloads

When an analysis profile is requested, the system SHALL include a `CycleRecorder` section with one observation for each cycle baseline recorder invocation in the profiled analysis, including calls whose evidence is empty or whose recording is disabled. The section SHALL report invocation counts by strict/audit context, enabled/disabled state, and empty/nonempty evidence state. Each observation SHALL retain the joint values for graph vertex count (`V`), full-graph edge count (`E`), evidence item count with multiplicity (`B`), unique source/target evidence-pair count (`U`), and emitted candidate count (`K`), plus total recorder elapsed time, SCC-index-build elapsed time, evidence-check/emission elapsed time, allocated bytes, and deterministic work counters.

The profile SHALL represent no recorder calls as an empty observation list with zero invocations. Calls that are disabled or have empty evidence SHALL report zero SCC-index and candidate-evaluation work. Elapsed time and allocated bytes SHALL be explicitly identified as environment-dependent, and their units SHALL be stable and documented. Existing process-level peak working-set and total-allocation measurements SHALL retain their existing meaning.

Cycle recorder instrumentation SHALL be collected only when profiling is explicitly enabled through the existing CLI or Testing API profile path. It SHALL NOT change cycle results, baseline candidate order, multiplicity, identity, findings, or exit status. The profile SHALL contain only bounded numeric workload/work data and strict/audit context; it SHALL NOT include layer/type names, diagnostic identities, repository paths, or serialized graph/evidence topology. The system SHALL NOT upload these observations automatically.

#### Scenario: A profiled cycle invocation retains its joint workload and split timings
- **WHEN** an enabled, nonempty cycle baseline recorder invocation occurs during a profiled analysis
- **THEN** the profile contains one observation with its strict/audit context, `V`, `E`, `B`, `U`, `K`, total elapsed time, SCC construction time, evidence-check/emission time, allocation count, and deterministic work counters

#### Scenario: Disabled and empty invocations remain in the denominator
- **WHEN** a profiled analysis reaches the recorder with disabled recording or empty evidence
- **THEN** the profile retains the invocation and state, with zero SCC-index and candidate-evaluation work

#### Scenario: A profiled run without cycle recorder calls reports an empty workload
- **WHEN** profiling is enabled but no cycle baseline recorder invocation occurs
- **THEN** the profile reports zero invocations and an empty observation list

#### Scenario: Profiling remains opt-in and semantics-preserving
- **WHEN** the same analysis is run with and without explicit profile collection
- **THEN** findings, complete diagnostic identities, ordering, baseline candidates, and exit status are identical, and the unprofiled run collects and writes no cycle recorder observations

#### Scenario: Recorder profile output excludes topology and identity data
- **WHEN** a cycle recorder profile section is serialized
- **THEN** it contains no layer/type names, diagnostic identities, repository paths, or raw graph/evidence topology, and no observation is sent to a remote service
