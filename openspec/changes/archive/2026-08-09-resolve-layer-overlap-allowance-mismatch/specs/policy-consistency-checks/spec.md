## MODIFIED Requirements

### Requirement: Overlapping layer definitions are detected
The system SHALL detect when a concrete type or namespace is matched by more than one internal layer definition without an acknowledged reconciliation, and SHALL report the conflict with the matched layer names and a representative concrete type/namespace. A pair is reconciled, and SHALL NOT be reported, when either layer declares the other in its `overlaps_with` list (see `layer-contracts`).

#### Scenario: Two internal layers match the same type
- **WHEN** a concrete type's namespace matches the patterns of two internal (non-external) layers, neither declares the other in `overlaps_with`, and no containment relationship reconciles the overlap
- **THEN** the policy-consistency check SHALL report a finding listing both layer names and that type as the representative concrete type/namespace

#### Scenario: External layer overlap is not a conflict
- **WHEN** a concrete type's namespace matches both an internal layer and a layer marked `external: true`
- **THEN** the policy-consistency check SHALL NOT report a layer-overlap finding for that type

#### Scenario: Overlap acknowledged via overlaps_with is not a conflict
- **WHEN** a concrete type's namespace matches the patterns of two internal layers and one of the two declares the other's name in its `overlaps_with` list
- **THEN** the policy-consistency check SHALL NOT report a layer-overlap finding for that pair
