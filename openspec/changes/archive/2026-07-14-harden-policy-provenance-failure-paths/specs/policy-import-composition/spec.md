## ADDED Requirements

### Requirement: Root parsing failures retain typed source provenance
The selected root SHALL receive a root source descriptor before YAML parsing
decides whether it contains imports. A malformed, multi-document, or non-mapping
root SHALL fail with a typed source-shape diagnostic whose location identifies
that root path and root role.

#### Scenario: Malformed selected root
- **WHEN** the explicitly selected root YAML is syntactically malformed
- **THEN** loading fails with a typed source-shape diagnostic for the root

#### Scenario: Root is not one mapping document
- **WHEN** the explicitly selected root contains multiple documents or a
  non-mapping document
- **THEN** loading fails with a typed source-shape diagnostic for the root

### Requirement: Raw composed YAML validation retains source provenance
Raw YAML validation that must run before DTO deserialization SHALL execute with
the composed node's provenance as its active validation subject. An imported
raw layer, contextual-contract, port-boundary, or semantic-coverage validation
failure SHALL produce a typed semantic-validation diagnostic for the fragment
node that introduced it.

#### Scenario: Imported blank layer namespace
- **WHEN** an imported fragment declares a layer namespace containing only
  whitespace
- **THEN** loading fails with a typed semantic-validation diagnostic identifying
  that fragment layer

#### Scenario: Imported raw selector field is invalid
- **WHEN** an imported contextual or port-boundary contract contains a raw
  selector field rejected before DTO deserialization
- **THEN** loading fails with a typed semantic-validation diagnostic identifying
  that fragment contract

### Requirement: Internal provenance identity is collision-safe
The provenance index SHALL use escaped JSON Pointer identity for effective
nodes. Display-oriented dot/index YAML paths SHALL be retained only in
`ArchitecturePolicySourceLocation.YamlPath` and SHALL NOT be used for lookup or
ownership binding. Legal mapping keys containing dots, brackets, numeric text,
`~`, or `/` SHALL not overwrite another node's provenance.

#### Scenario: Dot-containing layer key does not collide with nested path
- **WHEN** a root contributes layer key `a.namespace` and a fragment contributes
  layer `a` with a `namespace` property
- **THEN** an effective-schema error for `a.namespace` identifies the root
  declaration rather than the fragment property

#### Scenario: Escaped mapping key retains its source
- **WHEN** a legal composed mapping key contains `~` or `/`
- **THEN** its provenance lookup resolves to the source that declared that key

