## ADDED Requirements

### Requirement: Semantic evidence is additive to legacy coverage output
The system SHALL emit exclusion evidence only when it is present, preserving the legacy human and JSON shape for non-semantic exclusions and the existing two-argument exclusion-item constructor.

#### Scenario: Legacy exclusion has no evidence field
- **WHEN** a non-semantic coverage exclusion is rendered
- **THEN** human output has no empty evidence suffix and JSON contains only `item` and `reason`

### Requirement: Contextual selector evidence is canonical
The system SHALL render contextual selector metadata values recursively with sequence contents and invariant-culture scalar values.

#### Scenario: Sequence selector values are actionable
- **WHEN** a contextual selector has `metadata: { domain: [Sales, Inventory] }`
- **THEN** its evidence includes `[Sales,Inventory]`, not a CLR collection type name
