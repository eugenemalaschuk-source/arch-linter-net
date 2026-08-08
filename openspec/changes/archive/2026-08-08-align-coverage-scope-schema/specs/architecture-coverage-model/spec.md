## MODIFIED Requirements

### Requirement: Coverage scope is declared via a discriminant field
Each coverage contract SHALL declare exactly one `scope` value among `namespace`,
`project`, `assembly`, `dependency_edge`, `rule_input`, or `semantic_role`, with
scope-specific fields populated only for the declared scope. Namespace coverage
SHALL declare `roots`; project and assembly coverage SHALL classify discovery-wide
units and SHALL reject `roots`; dependency-edge coverage SHALL declare `between`;
and rule-input coverage SHALL declare `contract_ids`.

#### Scenario: Namespace-scope contract declares roots
- **WHEN** a coverage contract declares `scope: namespace`
- **THEN** it SHALL declare `roots` using the same `namespace`/`namespace_suffix`
  glob syntax already accepted by layer definitions

#### Scenario: Project and assembly coverage is discovery-wide
- **WHEN** a coverage contract declares `scope: project` or `scope: assembly`
- **THEN** it SHALL be valid without `roots` and SHALL reject a declared `roots`
  field as invalid for that scope

#### Scenario: Dependency-edge-scope contract declares layer pairs
- **WHEN** a coverage contract declares `scope: dependency_edge`
- **THEN** it SHALL declare `between` as a list of declared-layer-name pairs
