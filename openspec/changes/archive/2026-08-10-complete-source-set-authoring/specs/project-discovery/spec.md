## ADDED Requirements

### Requirement: Filtered solution inventory is available to project source sets
The system SHALL make repository-relative project paths from the final solution discovery result,
after include/exclude filtering, available as the project-kind source-set universe before project
metadata contracts are executed. This availability SHALL not require resolving project build
outputs.

#### Scenario: A newly discovered matching production project joins the set
- **WHEN** a new production project is added to the solution and matches an existing project-set
  selector after filtering
- **THEN** the resolved project-set inventory includes the new project without an authored project
  list update
