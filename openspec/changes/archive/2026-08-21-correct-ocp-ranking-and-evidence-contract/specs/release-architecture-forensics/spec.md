## MODIFIED Requirements

### Requirement: Stable rankings and refactoring investigations
Within one primary-category cohort, file findings SHALL rank by:

1. descending canonical score;
2. descending ordinary canonical TaskKey spread;
3. descending churn;
4. descending commit count;
5. ascending canonical path by scalar-value ordering.

This total order SHALL apply to hotspots, bottlenecks, and OCP-pressure
findings. Cross-category file findings remain grouped in canonical category
order.

Within one endpoint-category cohort, `G0` pairs rank by descending canonical
combined weight, commit component, task component, then canonical paths. Clusters
use the exact maximum/aggregate/path order above. Cross-cohort pair/cluster
results remain grouped.

Candidates are evidence-derived investigations, not automatic redesign decisions.
They retain source finding IDs, evidence/components, effective thresholds,
category/cohort identity, and caveats. Cluster-derived candidate logic uses
`Gtheta`; file scores remain `G0`-derived.

#### Scenario: Same-cohort total order
- **WHEN** same-cohort file findings tie on all numeric dimensions
- **THEN** canonical scalar-value path ordering is the final discriminator

#### Scenario: OCP score tie uses ordinary file evidence
- **WHEN** two same-category OCP findings have equal canonical score but
  different ordinary canonical TaskKey spread, churn, or commit count
- **THEN** they order by those dimensions before canonical path, even when path
  ordering would produce the opposite result

### Requirement: Auditable OCP-pressure finding projection
Release Architecture Forensics SHALL project one OCP-pressure finding for every
retained, non-ignored canonical logical file, grouped and ranked only within its
primary path category. Each finding SHALL expose the canonical path and aliases,
pathname-reuse limitation, canonical TaskKeys, pair-exclusive TaskKey-pair
provenance, SHA-deduplicated qualifying commit IDs per participating TaskKey,
repeated-edit total, matched ASCII role tokens, raw `G0` incident degree
evidence, normalized score components, and validated effective OCP weights.
Canonical JSON SHALL include the OCP finding groups and this evidence after all
fail-closed ingestion validation succeeds.

Findings SHALL describe heuristic `OCP pressure` or a `likely OCP violation`
with caveats and SHALL NOT claim a formal design-principle proof. Missing task,
role, or graph evidence SHALL contribute canonical zero without reweighting any
enabled OCP component.

#### Scenario: Ignored logical file
- **WHEN** analysis ignores remove a canonical logical file before `G0` and
  score-population construction
- **THEN** no OCP-pressure finding is projected for that file

#### Scenario: Missing OCP evidence
- **WHEN** a retained canonical logical file has no independent TaskKey pair, no
  matching role token, and no `G0` incident edge
- **THEN** its repeated-edit, role-hint, and centrality components are all
  `0.000000000`, the configured weights are unchanged, and the finding remains
  available as a caveated OCP-pressure result

#### Scenario: Auditable multi-partner repeated editing
- **WHEN** one canonical TaskKey has independent pairs with multiple partners
  and one qualifying commit is pair-exclusive against more than one partner
- **THEN** the finding records that commit once for the TaskKey and computes its
  repeated-edit contribution from the SHA-deduplicated union
