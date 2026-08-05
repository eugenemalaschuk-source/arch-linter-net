## ADDED Requirements

### Requirement: Reproducible final post-optimization release evidence is published
The system SHALL provide a repeatable, explicitly-invoked post-optimization
benchmark harness that reuses the `analysis-profile/v1` envelope, phase
boundaries, synthetic large multi-host fixture, and scenario semantics of the
checked-in pre-optimization evidence. It SHALL publish separate checked-in
machine-readable post-optimization evidence and a human-readable comparison
report after `analysis-cache/v1` and bounded parallel scanning are available.
The evidence SHALL state the reference hardware, operating system, runtime,
configuration, source/package identity, and harness configuration, and SHALL
describe median and p95 figures only as evidence for that declared environment,
never as a hardware-independent performance contract.

#### Scenario: Post-optimization evidence remains comparable to the baseline
- **WHEN** the checked-in pre- and post-optimization evidence are compared
- **THEN** each matching scenario uses the same phase boundaries, records at
  least ten valid samples, separates preparation from analysis and output time,
  and retains raw or deterministic summarized profile evidence

#### Scenario: Release documentation can consume the evidence without benchmarking
- **WHEN** release documentation reads the final evidence report
- **THEN** it can identify the declared environment, source identity, matrix,
  median/p95 results, correctness evidence, and non-universality statement
  without running the hardware-sensitive harness

### Requirement: Post-optimization evidence proves cache and parallel correctness
The post-optimization harness SHALL measure cache-disabled, first-population,
and verified warm-hit executions; sequential execution and documented bounded
parallel execution; separate and combined strict/audit execution; and one- and
three-sink output execution. Before accepting any successful timing sample, it
SHALL verify the expected completion status, CLI exit category, and output
publication state. It SHALL prove that cached and uncached canonical findings
and ordering are equivalent; sequential and parallel canonical findings and
ordering are equivalent; combined execution performs one policy composition,
one project evaluation, and no redundant target-assembly scan; cache profiles
identify avoided work and deterministic hit/miss/reject reasons; and parallel
profiles expose bounded observed concurrency and resource measurements where
the platform supports them.

#### Scenario: A verified warm cache hit is measured only after population
- **WHEN** the post-optimization matrix measures cache behavior
- **THEN** it records disabled and first-population runs separately from warm
  hits, and accepts a warm-hit sample only when its profile reports the exact
  avoided work and a verified cache-hit outcome

#### Scenario: Parallel execution preserves canonical results
- **WHEN** sequential and bounded-parallel runs use the same immutable inputs
- **THEN** their canonical findings and ordering are identical, observed
  concurrency does not exceed the resolved bound, and their profiles retain
  explicit resource-metric availability information

#### Scenario: Unsuccessful runs do not become timing samples
- **WHEN** a cancellation, failure, partial publication, or incorrect exit
  category occurs during the post-optimization matrix
- **THEN** the run is excluded from successful timing statistics while its
  completion, cleanup, cache, and concurrency evidence remains recorded
