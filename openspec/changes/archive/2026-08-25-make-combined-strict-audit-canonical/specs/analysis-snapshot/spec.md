## ADDED Requirements

### Requirement: Combined CLI modes share one ensure-built preparation

The CLI `validate` command invoked with `--mode strict,audit --ensure-built` SHALL create exactly one `ArchitectureAnalysisSnapshot` and use its one snapshot-owned build-state preparation for both requested modes. Any required post-build receipt verification SHALL remain part of that preparation; the second mode SHALL NOT initiate a second build, project-graph preparation, or snapshot. After both modes have been evaluated, the snapshot counters SHALL report one policy composition and two evaluated modes. Each result and the aggregate command exit category SHALL be equivalent to evaluating the corresponding standalone mode against the same verified build state.

#### Scenario: Combined ensure-built validation evaluates strict and audit

- **WHEN** the CLI validates one policy with `--mode strict,audit --ensure-built`
- **THEN** it performs one snapshot-owned preparation, evaluates both modes from
  that snapshot, preserves each standalone mode's result, and exits successfully
  only when both requested modes pass

### Requirement: Additional combined-mode reports consume completed outcomes

When a combined CLI validation routes human, JSON, and/or SARIF reports, every requested sink SHALL render the already-completed strict and audit outcomes from the one snapshot. Adding report sinks SHALL change only rendering and output evidence; it SHALL NOT compose policy, prepare the project graph, materialize a second analysis session, or execute either mode again.

#### Scenario: Combined validation routes multiple report formats

- **WHEN** a `--mode strict,audit` validation requests JSON and SARIF report
  sinks in addition to its normal output
- **THEN** the report artifacts contain the two completed mode results and the
  profile's analysis counters remain those of the one shared snapshot
