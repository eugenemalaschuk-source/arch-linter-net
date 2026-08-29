## MODIFIED Requirements

### Requirement: Family applicability matrices are explicit
The shared design SHALL define the following v0.8 applicability matrices for
the families that opt in:

| Family | Required evidence | Evaluable condition | Unassessable examples |
| --- | --- | --- | --- |
| Declared topology (#91) | declared topology control, observed subject universe, mapping and declaration evidence | every required observed subject is deterministically mapped or explicitly reviewed out of scope and declarations are current | missing subject universe, unmapped or ambiguous required subject, stale declaration |
| Contract-surface exposure (#92) | selected contract surface, visible signature/metadata facts, source and target classification evidence | the selected surface and required facts resolve completely for the configured control | missing selected surface, unresolved required fact, unexpected empty selector, stale declaration |
| Metrics and budgets (#93) | metric definition, target subject universe, and the measurement facts required by that metric | the metric's native counting universe and contributors are complete enough to trust its value | incomplete or unmapped target scope, ambiguous component, missing required measurement fact, unexpected empty input |
| External static diagnostics (#95) | logical evidence requirement, a bounded SARIF reader outcome, and required trust binding | one successful matching reader outcome proves configured producer/run, logical evidence key, repository, revision, scope, and artifact hash | missing, malformed, failed, over-limit, wrong-key, wrong-repository, wrong-revision, or wrong-scope required input |

Each family SHALL make its own exact subject and declaration semantics explicit
when it implements a control; it SHALL use this matrix rather than inventing a
parallel result envelope. The external-diagnostics family SHALL convert the
reader's typed trust outcome to its applicability record without deriving
evaluable state from an empty diagnostics collection.

#### Scenario: Valid current SARIF with no selected findings
- **WHEN** an external-diagnostics control receives a valid, successful,
  current-context SARIF run with zero selected diagnostics
- **THEN** its record is `evaluable` and distinguishes that state from a missing
  or stale required run

#### Scenario: Optional external evidence is not a failed required run
- **WHEN** an explicitly optional external-evidence control has no supplied
  artifact
- **THEN** its record is `not_applicable` with optional-policy provenance, not
  `unassessable` and not a successful zero-result run
