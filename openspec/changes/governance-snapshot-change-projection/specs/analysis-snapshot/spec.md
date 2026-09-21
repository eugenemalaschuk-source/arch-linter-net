## ADDED Requirements

### Requirement: One caller-owned snapshot can serve a complete governance projection
The system SHALL allow a composite caller that owns one
`ArchitectureAnalysisSnapshot` to evaluate the requested Health modes, verify
baseline candidates from the snapshot's completed analysis receipts, and
request the canonical current change projection before disposing that snapshot.
The composite path SHALL NOT compose policy, prepare the project graph, load
target assemblies, or create a second runner/session for baseline or graph
projection. The caller SHALL retain ownership and SHALL dispose the snapshot
after every requested projection completes.

#### Scenario: Health and change snapshot share one candidate preparation
- **WHEN** a caller requests Health and a current change snapshot from one
  caller-owned snapshot
- **THEN** Health, baseline comparison, and namespace/assembly change
  projections consume that snapshot's retained runner/session
- **AND** the resulting snapshot is complete and uses the canonical projector
  and serializer

#### Scenario: Shared projection preserves graph-specific semantics
- **WHEN** the composite change projection builds namespace and assembly views
- **THEN** it preserves the standalone graph workflow's mode, contract
  selection, `includeAsmdefContracts` behavior, coverage summaries, source
  expansion, and selector participation

#### Scenario: Incomplete shared evidence fails closed
- **WHEN** preflight is blocked, a required validation receipt is absent, or
  baseline candidates cannot be collected from the snapshot
- **THEN** the composite caller reports failure and does not write a partial
  change snapshot
