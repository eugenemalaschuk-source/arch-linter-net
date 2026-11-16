# Spec Delta

## ADDED Requirements

### Requirement: Audit scale evidence proves metric evaluation and independent contract families

Metric-rule workloads SHALL execute metric measurement and retain its result count and deterministic result identity in benchmark evidence. Ordinary and method-body contract counts SHALL reflect their declared dimensions independently.

#### Scenario: Metric rule scale performs metric work

- **WHEN** a metric-rule workload is measured
- **THEN** the analysis snapshot evaluates the declared metrics
- **AND** evidence records the number and deterministic values of the computed metrics
- **AND** incomplete or unassessable metric evaluation fails the workload

#### Scenario: Method-body rules do not inflate ordinary contracts

- **WHEN** method-body rule count changes while ordinary contract count remains fixed
- **THEN** ordinary dependency contracts remain at the declared ordinary contract count
- **AND** method-body contracts and total inventory reflect their own declared counts

### Requirement: Report-rule scale keeps finding volume fixed

Comparable report-rule workloads SHALL use a fixed finding volume and SHALL reference every declared report rule.

#### Scenario: Rule-count comparison preserves finding count

- **WHEN** report-rule counts of 1, 8, and 32 are measured
- **THEN** each SARIF result contains exactly 32 findings
- **AND** each declared report rule is referenced by at least one finding

### Requirement: History ancestry inputs represent merges and renames

Ancestry workload inputs SHALL represent diverging branches that rejoin at commits with two distinct parents. Rename-chain workloads SHALL contain explicit, continuous rename events, and expected ancestry answers SHALL be derived from the generated parent graph.

#### Scenario: Merged-DAG queries use real side branches

- **WHEN** a merged-DAG workload is generated
- **THEN** each declared merge has two distinct parent edges
- **AND** queried side-branch commits are reachable from the selected tip through the generated graph

#### Scenario: Long rename-chain inputs contain rename events

- **WHEN** a long rename-chain workload is generated
- **THEN** it contains the declared number of path-changing rename events
- **AND** each event's source path matches the preceding event's target path

### Requirement: Materialized input identity is reproducible across equivalent builds

The canonical materialized-input identity SHALL depend on canonical source/configuration inputs and explicitly staged analysis inputs, not temporary root paths or incidental compiler outputs.

#### Scenario: Equivalent builds have the same materialized-input identity

- **WHEN** the same workload is independently materialized and built twice
- **THEN** both materialized-input identities are identical
- **AND** generated `bin`/`obj` outputs and temporary directory names do not affect the identity
- **AND** explicitly staged analysis assemblies remain part of the identity
