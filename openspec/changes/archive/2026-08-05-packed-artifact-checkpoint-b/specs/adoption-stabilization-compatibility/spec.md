## MODIFIED Requirements

### Requirement: One public 0.5.1 release boundary
The system SHALL expose one public stabilization release boundary, `0.5.1`,
governed by compatibility envelope `adoption-stabilization/v1`. Checkpoint A is
internal integration evidence only; only a complete, reproducible Checkpoint B
packed-artifact acceptance matrix and release-evidence summary may authorize
publishing 0.5.1.

#### Scenario: Checkpoint A succeeds
- **WHEN** the safety-critical Checkpoint A scenarios pass
- **THEN** development may continue but no intermediate public stabilization
  version is implied or authorized

#### Scenario: Unrelated design slices progress independently
- **WHEN** one child task has an approved applicable design slice and another
  unrelated slice remains open
- **THEN** the first task may proceed without redefining the unresolved slice or
  waiting for the complete issue to close
