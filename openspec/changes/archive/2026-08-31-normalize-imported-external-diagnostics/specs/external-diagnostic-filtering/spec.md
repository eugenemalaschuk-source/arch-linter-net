## ADDED Requirements

### Requirement: Selected diagnostics have one non-revalidating normalization consumer
The trusted external-diagnostic selection result SHALL be consumable by a normalized-finding
projector that preserves its immutable source and evidence provenance. The consumer SHALL treat
#520 trust status and #521 selection identity as authoritative and SHALL NOT accept a replacement
mutable filter, reopen SARIF bytes, or turn a rejected artifact into a selected finding.

#### Scenario: Normalization cannot reauthorize stale evidence
- **WHEN** an artifact was rejected for a wrong required revision
- **THEN** normalization records the corresponding applicability evidence but cannot project its
  source results as a current governed finding
