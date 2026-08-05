## MODIFIED Requirements

### Requirement: Reproducible final post-optimization release evidence is published
The system SHALL provide a repeatable, explicitly-invoked post-optimization benchmark harness that reuses the `analysis-profile/v1` envelope, phase boundaries, synthetic large multi-host fixture, and scenario semantics of the checked-in pre-optimization evidence. It SHALL publish separate checked-in machine-readable post-optimization evidence and a human-readable comparison report after `analysis-cache/v1` and bounded parallel scanning are available. The evidence SHALL state the reference hardware, operating system, runtime, configuration, exact source commit, executed CLI binary file version and SHA-256, and the CLI package ID, semantic version, and SHA-256 digest of the one packed `.nupkg` selected by the harness. It SHALL describe median and p95 figures only as evidence for that declared environment, never as a hardware-independent performance contract.

#### Scenario: Post-optimization evidence remains comparable to the baseline
- **WHEN** the checked-in pre- and post-optimization evidence are compared
- **THEN** each matching scenario uses the same phase boundaries, records at least ten valid samples, separates preparation from analysis and output time, and retains raw or deterministic summarized profile evidence

#### Scenario: Release documentation can consume the evidence without benchmarking
- **WHEN** release documentation reads the final evidence report
- **THEN** it can identify the declared environment, source identity, executed-binary identity, packed-package identity, matrix, median/p95 results, correctness evidence, and non-universality statement without running the hardware-sensitive harness

### Requirement: Final release evidence is an attributable pre/post comparison
The final post-optimization evidence SHALL retain both strict and audit profiles for paired runs, raw wall-clock/allocation/resource samples, median and p95 summaries, exact source commit, executed CLI binary identity, CLI package ID/version/SHA-256 identity, explicit build configuration, and a #374 baseline-to-post-to-delta table. The harness SHALL fail rather than select zero or multiple matching CLI packages. It SHALL compare every cached sample with its uncached canonical baseline and every parallel sample with its sequential counterpart, including finding order, completion, exit/publication state, and deterministic counters.

#### Scenario: Release documentation consumes the comparison
- **WHEN** release documentation reads the checked-in final report
- **THEN** it can inspect raw profiles, distributions, source/binary/package identities, configuration, and baseline/post/delta without recalculating the dataset
