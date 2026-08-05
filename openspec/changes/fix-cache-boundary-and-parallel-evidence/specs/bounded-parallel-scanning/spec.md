## ADDED Requirements

### Requirement: Parallel activation is evidenced by completed bounded fact work
The system SHALL report `Active` concurrency only when a bounded fact-index/scanning phase schedules and completes real eligible partitions. A release benchmark parallel sample SHALL require max parallelism greater than one, at least four scheduled and completed work items, observed concurrency at least two, deterministic merge operations, and fact-index materialization.

#### Scenario: Empty workload does not prove parallel execution
- **WHEN** a run has no eligible fact work
- **THEN** it remains `NotApplicable` and cannot satisfy a parallel-evidence gate
