## ADDED Requirements

### Requirement: Auditable OCP-pressure finding projection
Release Architecture Forensics SHALL project one OCP-pressure finding for every
canonical logical file, grouped and ranked only within its primary path category.
Each finding SHALL expose the canonical path and aliases, pathname-reuse
limitation, canonical TaskKeys and match provenance, independent TaskKey pairs,
SHA-deduplicated qualifying commit IDs per participating TaskKey, repeated-edit
total, matched ASCII role tokens, raw `G0` incident degree evidence, normalized
score components, and validated effective OCP weights. Canonical JSON SHALL
include the OCP finding groups and this evidence after all fail-closed ingestion
validation succeeds.

Findings SHALL describe heuristic `OCP pressure` or a `likely OCP violation`
with caveats and SHALL NOT claim a formal design-principle proof. Missing task,
role, or graph evidence SHALL contribute canonical zero without reweighting any
enabled OCP component.

#### Scenario: Missing OCP evidence
- **WHEN** a canonical logical file has no independent TaskKey pair, no matching
  role token, and no `G0` incident edge
- **THEN** its repeated-edit, role-hint, and centrality components are all
  `0.000000000`, the configured weights are unchanged, and the finding remains
  available as a caveated OCP-pressure result

#### Scenario: Auditable multi-partner repeated editing
- **WHEN** one canonical TaskKey has independent pairs with multiple partners
  and one qualifying commit is pair-exclusive against more than one partner
- **THEN** the finding records that commit once for the TaskKey and computes its
  repeated-edit contribution from the SHA-deduplicated union
