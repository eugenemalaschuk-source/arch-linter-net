## ADDED Requirements

### Requirement: Public API snapshot operations can explicitly prepare receipt-backed artifacts

The system SHALL allow public API surface operations to request the existing
explicit ensure-built preparation mode and optional no-restore behavior. After a
successful preparation, the operation SHALL rebuild its runner from the
post-build artifact state and run ordinary receipt verification before scanning
or writing a snapshot. Without explicit preparation, public API operations
SHALL retain ordinary fail-closed preflight behavior.

#### Scenario: Prepared public API capture from an ordinary build state
- **WHEN** a public API capture targets a discovered project graph with missing,
  stale, or receiptless artifacts and requests ensure-built
- **THEN** the system SHALL build the selected graph, publish and verify the
  receipt, re-resolve the target artifacts, and capture from the verified
  post-build state

#### Scenario: Ordinary public API operation remains fail closed
- **WHEN** a public API operation targets a receiptless artifact without
  explicit preparation
- **THEN** the system SHALL report an `unverifiable-artifact` preflight
  diagnostic and SHALL NOT capture, compare, or update a snapshot

#### Scenario: Prepared public API operation honours no-restore
- **WHEN** a public API operation requests both ensure-built and no-restore
- **THEN** the preparation build SHALL not restore dependencies and SHALL report
  a typed prerequisite failure when restore is required
