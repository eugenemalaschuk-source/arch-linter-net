## ADDED Requirements

### Requirement: Layout conventions can opt into a bounded applicability inventory

The system SHALL allow a strict or audit layout-convention applicability inventory to declare a
bounded source-directory scope and a reviewed list of expected folder entries. Each expected
folder entry SHALL have a stable id, an exact normalized path under the declared scope, and the
id of one layout-convention control that governs it. The referenced control SHALL exist in the
same policy and applicability inventories SHALL use only source facts collected from configured
analysis source roots; they SHALL NOT discover arbitrary repository paths.

#### Scenario: Expected convention folder is present
- **WHEN** an inventory declares an expected `Application/Services` folder linked to a layout
  convention and source facts contain a subject from that normalized folder
- **THEN** the expected folder entry SHALL be recorded as present for that inventory

#### Scenario: Expected convention folder disappears
- **WHEN** an inventory declares an expected folder but no observed source-fact subject belongs
  to that normalized folder
- **THEN** the inventory SHALL produce deterministic stale-declaration applicability evidence
  naming the expected entry and its linked convention control

#### Scenario: Inventory path outside its scope is rejected
- **WHEN** an expected folder path is outside the inventory's declared bounded scope
- **THEN** policy loading SHALL reject the inventory with an actionable configuration error

### Requirement: Exhaustive layout inventory proves convention mapping completeness

The system SHALL allow an applicability inventory to explicitly declare `exhaustive: true`. For
an exhaustive inventory, every observed source-fact subject under the inventory scope SHALL map
to exactly one expected folder entry; subjects outside the scope SHALL NOT participate. A subject
that maps to no expected entry SHALL produce unmapped-subject applicability evidence, and a
subject that maps to more than one mutually exclusive expected entry SHALL produce
ambiguous-subject applicability evidence.

#### Scenario: In-scope folder is not inventoried
- **WHEN** an exhaustive inventory observes a source-fact subject under its scope whose folder
  is not represented by an expected entry
- **THEN** the inventory SHALL produce deterministic unmapped-subject applicability evidence

#### Scenario: Unrelated folder is outside scope
- **WHEN** source facts contain a folder outside the inventory's declared scope
- **THEN** that folder SHALL NOT produce an unmapped or ambiguous inventory result

#### Scenario: Subject maps to mutually exclusive entries
- **WHEN** one observed source-fact subject maps to multiple mutually exclusive expected folder
  entries in the same inventory
- **THEN** the inventory SHALL produce deterministic ambiguous-subject applicability evidence
  without choosing one entry implicitly

### Requirement: Inventory makes expected convention selection assessable

For every layout-convention control linked by an applicability inventory, the system SHALL prove
that the control selector matched at least one observed in-scope subject. A linked control whose
selector matches zero observed subjects SHALL produce unexpected-empty applicability evidence;
the result SHALL remain distinct from a configuration error and from an ordinary layout
violation. A policy that does not declare an applicability inventory SHALL retain existing
layout-convention evaluation semantics.

#### Scenario: Linked selector matches zero subjects
- **WHEN** an expected folder is present but its linked layout-convention selector matches no
  observed subjects
- **THEN** the shared applicability result SHALL report unexpected-empty evidence for that
  control instead of treating zero layout violations as proof of conformance

#### Scenario: Existing policy does not opt in
- **WHEN** a policy contains layout convention contracts but no applicability inventory
- **THEN** its source selection, strict/audit outcome, and output shape SHALL remain compatible
  with the existing layout-convention behavior

### Requirement: Layout inventory uses shared applicability evidence and output semantics

The system SHALL map layout inventory evidence to the canonical applicability expected-entry and
record model, using a stable control identity and policy provenance. Missing, stale, unexpected
empty, unmapped, and ambiguous evidence SHALL project through the existing normalized Human,
JSON, SARIF, Testing, and baseline pathways; the inventory SHALL NOT introduce a layout-specific
finding envelope or identity algorithm. An audit inventory SHALL report advisory drift without
making strict validation fail, while a strict inventory SHALL make valid-but-unassessable
applicability evidence fail closed according to the canonical assessment-completion semantics.

#### Scenario: Audit inventory reports advisory drift
- **WHEN** an audit applicability inventory has a stale expected folder
- **THEN** audit output SHALL contain the normalized applicability finding and strict validation
  SHALL not fail solely because of that audit evidence

#### Scenario: Strict inventory has incomplete evidence
- **WHEN** a strict applicability inventory has unmapped, ambiguous, stale, or unexpected-empty
  evidence
- **THEN** the canonical assessment result SHALL be unassessable and downstream outputs SHALL
  retain the inventory's stable identity and provenance
