## ADDED Requirements

### Requirement: Layer overlap can be explicitly acknowledged
A layer declaration MAY include `overlaps_with`, a list of other declared
layer names that this layer is intentionally allowed to match the same
concrete type as. Declaring the pairing on either of the two layers SHALL be
sufficient; the other layer need not repeat it.

#### Scenario: Overlap acknowledged from one side
- **GIVEN** layer `sales_domain` declares `overlaps_with: [audit_aspect]`
- **AND** layer `audit_aspect` does not declare `overlaps_with`
- **WHEN** a concrete type matches both `sales_domain` and `audit_aspect`
- **THEN** the pair is treated as an acknowledged overlap for that type

#### Scenario: Layer without overlaps_with is unchanged
- **GIVEN** a layer declared with no `overlaps_with` key
- **WHEN** the policy document loads
- **THEN** matching and overlap detection behave exactly as they did before
  this field existed

### Requirement: Layer overlap acknowledgment entries are validated
`overlaps_with` entries SHALL be non-empty strings, each referencing a layer
name declared elsewhere in `layers`. An entry referencing an undeclared layer
name, or a layer's own name, SHALL fail to load with an actionable error
naming the offending layer and entry.

#### Scenario: Undeclared layer name is rejected
- **GIVEN** a layer `sales_domain` with `overlaps_with: [nonexistent_layer]`
- **WHEN** the policy document loads
- **THEN** loading fails with an error naming `sales_domain` and
  `nonexistent_layer`

#### Scenario: Self-reference is rejected
- **GIVEN** a layer `sales_domain` with `overlaps_with: [sales_domain]`
- **WHEN** the policy document loads
- **THEN** loading fails with an error naming `sales_domain`
