## ADDED Requirements

### Requirement: The first wave approves no built-in annotation types
The semantic role catalog's first wave SHALL approve no ArchLinterNet-provided annotation types or annotation package. Annotation names in the catalog SHALL be candidates/examples only, and user-defined attributes mapped by full type name in YAML SHALL remain the supported adoption path. A future optional annotation package or source-only distribution SHALL require a separate product and packaging decision in issue #108.

#### Scenario: A reader evaluates an annotation example
- **WHEN** the catalog shows an annotation such as `[DomainLayer("Sales")]`
- **THEN** it identifies the annotation as a candidate/example rather than a shipped ArchLinterNet type and points to custom YAML mapping as the current supported path
