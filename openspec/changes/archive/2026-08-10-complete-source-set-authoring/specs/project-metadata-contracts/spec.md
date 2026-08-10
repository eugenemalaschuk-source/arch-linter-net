## ADDED Requirements

### Requirement: Project metadata reuses solution-derived project sets
The system SHALL allow project metadata contracts to use `project_sets` that resolve from the final
solution-discovered project inventory, without requiring the same paths to be duplicated in
`analysis.projects`. Explicit `analysis.projects` and explicit `projects` contract entries SHALL
remain supported and backward compatible.

#### Scenario: Excluded test projects cannot enter metadata governance
- **WHEN** solution discovery excludes test project paths and a metadata contract references a
  project set matching the production path shape
- **THEN** the contract does not govern an excluded test project
