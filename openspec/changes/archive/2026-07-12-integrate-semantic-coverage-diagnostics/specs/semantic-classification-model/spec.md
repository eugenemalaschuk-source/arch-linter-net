## MODIFIED Requirements

### Requirement: Classification interacts with coverage through an aligned, not parallel, vocabulary
The runtime SHALL integrate implemented semantic classification with the existing architecture-coverage-model through `scope: semantic_role`, using the shared `covered`/`excluded`/`uncovered`/`unknown`/`stale`/`empty-input` vocabulary rather than introducing a separate coverage-like diagnostic vocabulary. A discovered role/metadata fact SHALL count as consumed when matched by a `layers.<name>.selector` or referenced directly by an implemented contextual-contract selector; a role assigned by an override does not by itself exempt a type from coverage. Conflicts and metadata failures SHALL remain explainable classification evidence consumed by semantic coverage diagnostics.

#### Scenario: Uncovered semantic fact aligns with coverage output
- **WHEN** a role is discovered but consumed by no selector or contextual contract and named by no exclusion
- **THEN** a selected semantic-role coverage contract reports the fact as `uncovered`

#### Scenario: Contextual consumption counts as governance
- **WHEN** an implemented contextual contract references a discovered role/metadata value directly
- **THEN** semantic-role coverage treats the matching fact as covered in the same way as a layer selector match

#### Scenario: Classification conflicts remain distinct
- **WHEN** the role index reports conflicting classification sources for a fact
- **THEN** semantic coverage exposes conflict evidence separately from dependency violations and does not silently count the fact as governed
