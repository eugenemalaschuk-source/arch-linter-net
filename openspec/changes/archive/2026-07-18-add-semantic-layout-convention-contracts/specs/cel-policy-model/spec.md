## MODIFIED Requirements

### Requirement: First-wave expression locations are closed and selector-scoped

The first-wave model SHALL allow `when` only at these YAML locations:

- `layers.<name>.selector.when`
- contextual dependency contract `source.when`
- contextual dependency contract `forbidden[*].when`
- contextual dependency contract `exclude[*].when`
- contextual allow-only contract `source.when`
- contextual allow-only contract `allowed[*].when`
- contextual allow-only contract `exclude[*].when`
- `strict_layout_conventions[*].files_matching.when`
- `audit_layout_conventions[*].files_matching.when`

Every other YAML location SHALL forbid expressions, including `imports`,
analysis settings, coverage contracts, baseline files, classification mapping
entries, literal selector fields, contract IDs/names/reasons, and ordinary
list/scalar members in non-contextual, non-layout contract families.

#### Scenario: Expression refines a selector-backed layer

- **WHEN** `layers.sales.selector.when` is present
- **THEN** the selector remains valid only because `when` is attached to the
  selector node itself

#### Scenario: Contextual exclude has one unambiguous decision

- **WHEN** a contextual `exclude` selector declares `when`
- **THEN** the field is allowed and evaluated under the same typed target
  context as its surrounding contextual contract

#### Scenario: Expression in an unsupported location is rejected

- **WHEN** a policy author adds an expression-shaped value under
  `analysis.target_assemblies`, `classification.attributes[*].metadata`,
  `contracts.strict_coverage[*]`, or `imports[*]`
- **THEN** policy loading fails rather than silently accepting or ignoring that
  expression

#### Scenario: Expression refines a layout convention file selector

- **WHEN** `contracts.strict_layout_conventions[*].files_matching.when` or
  `contracts.audit_layout_conventions[*].files_matching.when` is present
- **THEN** the expression is allowed only because it is attached to the
  `files_matching` selector node itself, compiles against the existing
  `subject` context shape, and no other location on a `layout_conventions`
  contract (contract-level `reason`, `ignored_violations[*]`, naming/type-kind
  expectation fields) accepts `when`

#### Scenario: Expression on a non-selector layout convention field is rejected

- **WHEN** a policy author adds a `when` field under
  `contracts.strict_layout_conventions[*].require_matching_interface` or any
  other non-`files_matching` location on a `layout_conventions` contract
- **THEN** policy loading fails rather than silently accepting or ignoring that
  expression
