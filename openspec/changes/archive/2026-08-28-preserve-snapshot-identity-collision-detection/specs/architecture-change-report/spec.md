## ADDED Requirements

### Requirement: Semantic snapshot observations are deduplicated without suppressing identity collisions
The system SHALL collapse repeated semantic-role observations only when their subject, role, and complete metadata key/value set are structurally equivalent, independent of metadata enumeration order. It SHALL collapse repeated semantic-context observations only when their subject, metadata key, and typed metadata value are structurally equivalent. This projection SHALL occur before snapshot validation and SHALL NOT suppress, rewrite, or merge any remaining duplicate `(Kind, Identity)` pair produced by structurally different entries; such an identity collision SHALL continue to be rejected as an invalid snapshot.

#### Scenario: Equivalent linked-type observations are collapsed
- **WHEN** distinct CLR type instances from separate assemblies produce semantic-role observations with the same subject, role, and metadata values
- **THEN** the snapshot contains one semantic-role entry and one entry for each structurally distinct semantic context
- **AND THEN** the classification result still contains the per-assembly observations

#### Scenario: Metadata enumeration order does not create an extra semantic role surface
- **WHEN** two semantic-role observations have the same subject, role, and metadata key/value pairs in different enumeration orders
- **THEN** the snapshot contains one semantic-role entry for those observations

#### Scenario: Structurally different facts with a serialized identity collision fail closed
- **WHEN** two structurally different semantic-role observations serialize to the same `(Kind, Identity)` pair because their legacy identity encoding is ambiguous
- **THEN** snapshot serialization rejects the snapshot with the duplicate-or-empty entry-identity error
- **AND THEN** neither observation is silently removed or merged

