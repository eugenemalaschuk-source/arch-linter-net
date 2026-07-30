## ADDED Requirements

### Requirement: Checkpoint A reusable corpus is non-release evidence
The compatibility workflow SHALL maintain one reusable synthetic adopter corpus and deterministic scenario inventory for internal Checkpoint A evidence. The corpus SHALL cover small, conventional multi-project, multi-host, legacy-migration, and clean-checkout shapes, and SHALL be reused by profiling, consistency review, and final acceptance work. Checkpoint A evidence SHALL be explicitly non-release evidence and SHALL NOT authorize package publication or version 0.5.1.

#### Scenario: Checkpoint A evidence is recorded
- **WHEN** scoped adoption-critical scenarios pass on an observed platform
- **THEN** the recorded evidence identifies the exercised scenarios and platform while retaining Checkpoint B as the only release authorization gate
