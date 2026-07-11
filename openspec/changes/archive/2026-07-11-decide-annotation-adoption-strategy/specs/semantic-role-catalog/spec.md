## MODIFIED Requirements

### Requirement: The first wave approves no built-in annotation types
The semantic role catalog's first wave SHALL approve no ArchLinterNet-provided annotation types or annotation package. Annotation names in the catalog SHALL be candidates/examples only, and user-defined attributes mapped by full type name in YAML SHALL remain the supported adoption path. Issue #108 resolves the packaging decision: ArchLinterNet SHALL ship no binary annotation package and no source-only annotation package in this wave; user-defined attributes mapped by full type name in YAML remain the sole supported adoption path until a future, separately-decided change introduces an optional package.

#### Scenario: A reader evaluates an annotation example
- **WHEN** the catalog shows an annotation such as `[DomainLayer("Sales")]`
- **THEN** it identifies the annotation as a candidate/example rather than a shipped ArchLinterNet type and points to custom YAML mapping as the current supported path

#### Scenario: A reader looks for an annotation package
- **WHEN** a reader searches the catalog or policy-format documentation for an installable ArchLinterNet annotation package
- **THEN** the documentation states that no binary or source-only package exists in this wave, explains the trade-offs of user-owned attributes versus a future package, and confirms user-defined attributes mapped by full type name remain fully supported today
