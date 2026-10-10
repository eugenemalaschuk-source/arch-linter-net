## MODIFIED Requirements

### Requirement: Size metrics use explicit source identity semantics
The snapshot SHALL report `sourceLines`, `sourceFiles`, `projects`, `types`, and `publicTypes`. `sourceLines` and `sourceFiles` SHALL count every successfully read C# file within configured source roots after the existing generated-file exclusion, regardless of whether the file can be assigned to one target assembly. A file discovered through multiple source-root contexts SHALL count once by normalized repository-relative identity. Source declarations used to enrich type facts SHALL remain ownership-aware. Unreadable source evidence SHALL make source metrics partial or unavailable rather than fabricating a complete value; ambiguous assembly ownership alone SHALL NOT make a readable physical source inventory incomplete.

#### Scenario: Overlapping source roots do not multiply files
- **WHEN** the same physical C# file is discovered through more than one configured source root
- **THEN** it contributes one source file and one physical line count to the repository snapshot

#### Scenario: Generated and unreadable files are explicit
- **WHEN** a generated C# file is encountered or a configured source file cannot be read
- **THEN** generated code is excluded according to the existing generated-file policy
- **AND** unreadable source evidence makes the source metric partial or unavailable rather than fabricating a complete value

#### Scenario: Ambiguous project ownership does not erase physical source size
- **WHEN** readable non-generated C# files are under configured source roots but multiple discovered projects share the same owning directory
- **THEN** those files contribute once to `sourceFiles` and `sourceLines`
- **AND** declarations from those files remain unowned for type-source correlation
- **AND** the source-size metrics remain complete when the configured roots were fully readable and the other metric inputs are complete
