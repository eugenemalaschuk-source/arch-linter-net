## ADDED Requirements

### Requirement: Canonical identity requalification has an explicit baseline lifecycle
When a structured baseline identity gains required canonical qualification, documentation and
machine comparison results SHALL direct users to review `changed`, `stale`, or `ambiguous` entries
and explicitly update or recapture before pruning. The system SHALL not automatically approve,
rewrite, or broaden such entries.

#### Scenario: A user verifies a requalified baseline
- **WHEN** baseline verification encounters a structured identity that no longer exactly matches after requalification
- **THEN** verification SHALL fail with the structured lifecycle status and the user SHALL be able to follow a documented review, update/recapture, and prune path.
