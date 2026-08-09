## ADDED Requirements

### Requirement: Public API snapshot workflow is reachable from a normal repository state

The system SHALL provide a documented supported command sequence in which a
public API snapshot can be captured, diffed, and updated from a normal
repository state through explicit receipt-backed preparation. `diff` and
`update` SHALL be able to consume the resulting snapshot without an external
wrapper or a prerequisite command that removes its target artifacts.

#### Scenario: Capture, diff, and update use one prepared artifact state
- **WHEN** an installed or packed CLI runs public API capture with
  `--ensure-built`, then runs diff and update against the resulting snapshot
- **THEN** capture SHALL create the reviewed snapshot and diff/update SHALL
  complete from the resulting receipt-backed artifact state

#### Scenario: Stale artifacts are not accepted by snapshot commands
- **WHEN** the selected source or artifact state changes after its receipt was
  created and a public API operation runs without a successful new preparation
- **THEN** the operation SHALL fail with deterministic build-state diagnostics
  and SHALL NOT write a snapshot
