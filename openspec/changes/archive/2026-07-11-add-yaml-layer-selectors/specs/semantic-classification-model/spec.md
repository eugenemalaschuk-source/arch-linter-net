## MODIFIED Requirements

### Requirement: Selector syntax is additive to the existing layer shape, and selector-only layers are valid
The policy schema and runtime SHALL support `layers.<name>.selector` as an optional exact-match selector sibling to `namespace`/`namespace_suffix`/`external`. A selector SHALL require a non-empty `role` and MAY declare scalar metadata constraints. A layer SHALL declare either a non-empty `namespace` or a selector; when both are present, both predicates SHALL match. Namespace-only layers SHALL retain their existing behavior.

#### Scenario: Selector-only layer is accepted and resolves classified types
- **WHEN** a layer declares `selector: { role: DomainLayer }` without `namespace`
- **AND** a loaded type has the exact role `DomainLayer`
- **THEN** schema validation accepts the layer and layer lookup includes that type

#### Scenario: A layer may declare namespace and selector together
- **WHEN** a layer declares `namespace: MyApp.Domain` and `selector: { role: DomainLayer }`
- **THEN** a type must match both the namespace pattern and the semantic selector to belong to the layer

#### Scenario: Existing namespace-only layers are unaffected
- **WHEN** a layer declares only `namespace` (with optional glob, suffix, or external settings)
- **THEN** it is accepted and interpreted identically to its pre-selector behavior

#### Scenario: Selector metadata matching is exact and AND-combined
- **WHEN** a selector declares multiple metadata key/value constraints
- **THEN** every declared key must match the type descriptor exactly, with no wildcard or regex matching

## ADDED Requirements

### Requirement: Layer selector diagnostics are deterministic and explainable
The system SHALL reject invalid selector definitions with deterministic configuration diagnostics, and SHALL expose a deterministic empty-match diagnostic for a valid selector that matches no classified type unless the layer is external. Layer descriptions and relevant diagnostics SHALL identify semantic selection when a selector participates.

#### Scenario: Selector without a role is rejected
- **WHEN** a layer declares `selector` without a non-empty `role`
- **THEN** policy validation rejects the document with a selector configuration diagnostic

#### Scenario: Empty selector match is visible
- **WHEN** a non-external selector-backed layer matches no loaded type
- **THEN** configuration or coverage diagnostics report that the semantic selector matched no types

#### Scenario: External empty selector is suppressed consistently
- **WHEN** an external selector-backed layer matches no loaded type
- **THEN** the existing external-layer empty-layer suppression behavior is preserved

#### Scenario: Match diagnostics identify the matching mechanism
- **WHEN** a type is resolved into a selector-backed layer
- **THEN** layer descriptions or diagnostics can distinguish namespace matching, semantic selector matching, and their combination
