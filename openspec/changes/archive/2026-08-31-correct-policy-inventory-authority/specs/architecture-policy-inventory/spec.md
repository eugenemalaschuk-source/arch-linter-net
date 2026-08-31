## MODIFIED Requirements

### Requirement: Effective policy control inventory is canonical and deterministic
The system SHALL expose a versioned `architecture-policy-inventory/v1`
projection for an analyzed effective policy context. Its
`effective_rule_count` SHALL count one configured control per stable effective
contract identity after normal policy composition, condition resolution,
selection, and source-set expansion. A source-set-expanded authored contract
SHALL count once regardless of its executable alias fan-out. Findings, matched
subjects, files, types, edges, baseline entries, and waiver entries SHALL NOT
increase the rule count.

The inventory SHALL be repository-level authority over every selected effective
strict, audit, and coverage control, independent of the mode used to evaluate
findings for the current invocation. The inventory SHALL expose a deterministic
partition of the headline count into non-coverage `strict`, non-coverage
`audit`, and `coverage` controls. Disabled or optional-empty controls that do
not participate in the effective analyzed scope SHALL NOT be reported as
effective controls. A strict and an audit invocation with the same effective
policy selection and execution-scope exclusions SHALL expose identical
inventory counts.

#### Scenario: Source-set aliases count as one control
- **WHEN** one authored dependency contract expands to multiple source-set
  execution aliases in the selected validation scope
- **THEN** the inventory counts that contract once and repeated findings from
  any alias do not change `effective_rule_count`

#### Scenario: Composed strict audit and coverage controls have a stable partition
- **WHEN** a composed effective policy contains strict, audit, and coverage
  controls across imported fragments
- **THEN** the headline count and the strict/audit/coverage breakdown are
  deterministic and the three breakdown counts sum to the headline

#### Scenario: Selected scope does not imply unrelated controls
- **WHEN** validation selects only a subset of effective contract IDs
- **THEN** the inventory describes that exact selected analyzed scope and does
  not count unselected controls

#### Scenario: Invocation mode does not partition repository-level inventory
- **WHEN** strict and audit validation run against the same selected effective
  policy containing controls in both modes
- **THEN** each outcome exposes the same inventory with non-zero strict and
  audit counts rather than omitting the mode that was not evaluated for findings
