# Spec Delta

## MODIFIED Requirements

### Requirement: History ancestry inputs represent merges and renames

Ancestry workload inputs SHALL represent diverging branches that rejoin at commits with two distinct parents. Rename-chain workloads SHALL contain explicit, continuous rename events, and expected ancestry answers SHALL be derived from the generated parent graph. Ancestry query sets SHALL include both positive and negative answers, with each query carrying its expected answer. The summary SHALL report strict-ancestor closure cardinality derived from the graph, and SHALL NOT label that structural count as runtime retained-memory measurement.

#### Scenario: Merged-DAG queries use real side branches
- **WHEN** a merged-DAG workload is generated
- **THEN** each declared merge has two distinct parent edges
- **AND** the query set includes positive and negative ancestry cases
- **AND** an independent check derives each answer from the parent graph and matches the query's expected answer
- **AND** the strict-ancestor closure count equals the graph-derived cardinality
- **AND** the summary does not present this structural count as measured runtime retention

#### Scenario: Long rename-chain inputs contain rename events
- **WHEN** a long rename-chain workload is generated
- **THEN** it contains the declared number of path-changing rename events
- **AND** each event's source path matches the preceding event's target path
- **AND** the query set includes positive and negative ancestry cases whose expected answers match the parent graph
- **AND** the strict-ancestor closure count equals the graph-derived cardinality
- **AND** the summary does not present this structural count as measured runtime retention

## ADDED Requirements

### Requirement: Audit evidence identifies loaded assembly bytes separately from source inputs

Audit evidence SHALL retain the canonical source/configuration input digest separately from SHA-256 identities of the exact DLL bytes loaded for analysis. Loaded assembly identities SHALL be derived from the bytes consumed by the loader and SHALL not depend on temporary fixture paths.

#### Scenario: Audit evidence records exact loaded DLL identities
- **WHEN** a compiled audit workload is measured
- **THEN** the evidence retains its materialized source/configuration digest
- **AND** it separately records the loaded DLL inventory and each DLL's SHA-256 captured from the bytes loaded for analysis
- **AND** equivalent loaded binaries have the same identities across temporary fixture roots
