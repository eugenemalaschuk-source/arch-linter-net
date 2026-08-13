## ADDED Requirements

### Requirement: The packed gate executes a release-blocking public-API surface-selector consumer-exit matrix
The packed-artifact gate SHALL execute, against the candidate tool and packages installed from the
isolated local feed, a required scenario group proving that `surface_selector` on
`strict_public_api_surface`/`audit_public_api_surface` contracts lets a modular consumer replace a
whole-assembly reviewed API snapshot with a materially smaller intentional snapshot. The group
SHALL prove: the selected snapshot omits incidental exported types that an assembly-wide sibling
contract with no selector still governs; selection through a user-owned `has_attribute` marker and
through at least one other bounded selector source (`namespace`, `base_type`, `implements_interface`,
`layer`, or `role`) both produce a materially reduced snapshot; a selected type whose existing
semantic role is not `ApiContract` retains that role and remains governed by an ordinary role-based
contract unchanged; the exact snapshot comparison reports an added, removed, and changed selected
signature and `update` restores a clean comparison; adding or removing selector-matching evidence on
a type is observed as a review-visible snapshot delta; a selected member's signature referencing an
unselected first-party exported type fails closed instead of silently escaping; a full-policy strict
run over the fixture's permanent selector contracts is green; and the CLI and packaged
`ArchLinterNet.Testing` resolve the same effective selected surface and normalized findings for the
same contract.

#### Scenario: A selector regresses to whole-assembly behavior in the packed candidate
- **WHEN** the selected snapshot produced by the installed candidate contains an incidental exported
  type the selector should have excluded
- **THEN** the platform evidence records the snapshot-reduction scenario as failed and the
  aggregated evidence does not authorize publication

#### Scenario: A selected first-party escape is not rejected
- **WHEN** capturing or validating a selected contract whose member signature references an
  unselected first-party exported type does not fail
- **THEN** the platform evidence records the fail-closed-escape scenario as failed and the aggregated
  evidence does not authorize publication

## MODIFIED Requirements

### Requirement: Evidence is schema-backed and cannot self-authorize
The aggregation job SHALL validate exactly one evidence record for every required
platform, its observed architecture and shell, required scenario inventory,
consumer policy-shape counters, candidate package manifest, and independently produced
repository-gate results. It SHALL reject a platform record whose declared result contradicts its
own scenario results. The required scenario inventory SHALL include the public-API surface-selector
consumer-exit matrix alongside the existing consumer-cleanup matrix. It SHALL emit an immutable
GitHub Actions workflow artifact containing the candidate-manifest digest and workflow-run
reference, an explicit PASS or FAIL publication statement naming the candidate version, and the
inventory of failed scenarios and policy-shape defects; it SHALL NOT hard-code successful gates or
authorization. It SHALL terminate unsuccessfully when the verdict is FAIL. This artifact is the
authoritative release record and is retained according to the repository artifact-retention policy;
generated evidence is not checked into the source tree.

#### Scenario: Evidence is incomplete
- **WHEN** a platform record, required scenario, policy-shape counter, gate result, or manifest
  digest is absent, duplicated, mismatched, or invalid
- **THEN** aggregation fails and no authorization statement is emitted

#### Scenario: A required scenario failed
- **WHEN** any required scenario is recorded as failed on any platform
- **THEN** the emitted evidence states FAIL for the candidate version, lists the failed scenario
  and its reason, and the aggregation job terminates unsuccessfully
