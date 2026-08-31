# contract-surface-exposure-contracts Specification

## Purpose
Define declarative architecture rules that keep a selected visible .NET contract surface from exposing forbidden semantic or structural type targets.

## Requirements

### Requirement: Policies declare semantic contract-surface exposure controls
The system SHALL support `strict_contract_surface_exposure` and `audit_contract_surface_exposure` controls. Every control SHALL declare one bounded source-surface selection and at least one bounded forbidden-target selector. A source selection SHALL support an effective reviewed public-API surface by its declared contract identity, or direct selection by existing project, assembly, and structural or semantic type-selector evidence. A direct selector SHALL reuse the existing bounded `name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, and semantic `role` vocabularies; it SHALL NOT introduce a free-form expression, regex, tag, or alternate semantic-classification model.

All populated source-selection criteria SHALL combine conjunctively. Each forbidden selector SHALL combine its populated criteria conjunctively, while multiple forbidden selectors SHALL form a disjunction. Invalid, empty, unknown, or unbounded source/target selector configuration SHALL be rejected as invalid policy configuration rather than silently evaluating an empty rule.

#### Scenario: A role-selected API surface forbids entities
- **WHEN** a strict exposure control selects `Controller` source types with existing bounded selector evidence and forbids `Entity` targets through the existing semantic role index
- **THEN** the control evaluates the visible contracts of those controller types without changing either source or target primary semantic role

#### Scenario: Structural target selectors retain existing matching behavior
- **WHEN** a control forbids types selected by an existing namespace, base-type, interface, or attribute matcher
- **THEN** the selector uses the same bounded matching semantics as the existing structural type-selection capability

### Requirement: Effective reviewed API membership is consumed without reconstruction
When a source selection references an existing intentional reviewed public-API surface, the system SHALL use that contract's already-effective selected type membership and exported visible-surface semantics. It SHALL NOT rescan a snapshot, recreate API-membership selection, or add a second role/tag to any selected type. A referenced public-API surface with missing, ambiguous, stale, or empty required selection evidence SHALL be represented through the control's applicability result and SHALL NOT be treated as a complete zero-finding assessment.

#### Scenario: An API-selected value object retains its primary role
- **WHEN** a `ValueObject` enters a reviewed public-API surface through an orthogonal marker and that surface is selected by an exposure control
- **THEN** the exposure control evaluates the value object's visible contract while it remains a `ValueObject` for all semantic governance

### Requirement: Recursive exposure leaks produce deterministic, path-rich findings
For every selected visible source root, the system SHALL evaluate the recursive exposure evidence supplied by the contract-surface exposure index. If a referenced type matches any forbidden selector, the system SHALL emit one deterministic finding for each distinct source/path/target occurrence. The finding SHALL include the source surface, declaring source type, member or metadata site when present, a deterministic readable exposure path, and the forbidden target's assembly-qualified identity. Same-named target types from distinct assemblies and distinct paths to one target SHALL remain distinguishable.

#### Scenario: A nested generic signature exposes a forbidden domain type
- **WHEN** a selected visible API member returns `Task<Envelope<Customer>>` and `Customer` matches a forbidden target selector
- **THEN** a finding identifies the declaring member and a path through both generic wrappers to the assembly-qualified `Customer` target

#### Scenario: Visible metadata exposes a forbidden framework type
- **WHEN** a selected visible type, member, parameter, return value, or visible accessor metadata references a forbidden attribute type or typed attribute argument
- **THEN** a finding identifies the attribute or attribute-argument path rather than reporting only a coarse dependency

### Requirement: Exposure controls fail closed on incomplete or unexpectedly empty evidence
Each effective exposure control SHALL contribute one required applicability control. It SHALL be evaluable only when its selected source surface, forbidden-target selection, and required recursive exposure facts are complete. A required source or forbidden selector that matches zero current types, a referenced reviewed surface that yields zero governed roots, or incomplete required visible-signature/metadata evidence SHALL produce deterministic unassessable applicability evidence with canonical provenance and a bounded reason such as `unexpected_empty_input`, `stale_declaration`, or `missing_required_input`.

Zero architecture-leak findings SHALL be reported as a clean result only when that control's applicability evidence is evaluable. Applicability evidence SHALL use the shared canonical strict/audit, Human, JSON, SARIF, Testing, and baseline lifecycle rather than an exposure-specific result envelope or debt identity algorithm.

#### Scenario: A stale forbidden-role selector cannot create a false green
- **WHEN** a required forbidden selector resolves to no current target types
- **THEN** the control is unassessable with deterministic selector provenance and does not report a trusted clean assessment

#### Scenario: Incomplete reflection evidence cannot hide a leak
- **WHEN** the exposure index reports unavailable required first-party signature or metadata evidence for a selected source root
- **THEN** the control projects an unassessable applicability result even if its materialized exposure records contain no forbidden target

### Requirement: Exposure-contract findings participate in existing governance lifecycle
Strict and audit exposure findings SHALL preserve their normal mode-specific severity and participate in canonical identity, ignores, baseline comparison, normalized JSON, SARIF, and Testing output. Their stable identity SHALL use typed source, path, and target facts rather than rendered diagnostic prose. Existing dependency, type-placement, public-API snapshot, attribute-placement, and inheritance contracts SHALL retain their existing behavior and SHALL NOT be reinterpreted as exposure controls.

#### Scenario: An existing dependency policy remains unchanged
- **WHEN** a policy contains dependency, type-placement, or public-API snapshot controls but no contract-surface exposure control
- **THEN** its existing findings and snapshot behavior remain unchanged
