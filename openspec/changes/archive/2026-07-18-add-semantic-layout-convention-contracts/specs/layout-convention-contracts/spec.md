## ADDED Requirements

### Requirement: Declare layout convention contracts
The system SHALL allow `contracts.strict_layout_conventions` and `contracts.audit_layout_conventions` entries, each declaring a `files_matching` selector and at least one expectation among `require_type_kind`, `forbid_type_kind`, `required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, `forbidden_name_prefix`, `require_type_name_matches_file_name`, and `require_matching_interface`.

#### Scenario: Policy declares a layout convention contract
- **WHEN** a policy declares `contracts.strict_layout_conventions` with `files_matching.folder_segment: Services` and `require_type_kind: class`
- **THEN** the policy loader SHALL expose a `strict_layout_conventions` contract restricting declared types in files under a `Services` folder segment to type kind `class`

#### Scenario: Selector with no usable files_matching field is rejected
- **WHEN** a policy declares a `layout_conventions` contract with a `files_matching` node but no `folder_segment`, `namespace_segment`, `file_name_suffix`, or `file_name_prefix` populated
- **THEN** policy loading SHALL fail with a configuration error identifying the contract, because an empty selector would match every source file

#### Scenario: Selector with no expectation is rejected
- **WHEN** a policy declares a `layout_conventions` contract with a populated `files_matching` selector but no expectation field populated
- **THEN** policy loading SHALL fail with a configuration error identifying the contract, because the rule could never produce a violation

### Requirement: Match source files by constrained path selectors
The system SHALL select candidate source files for a `layout_conventions` contract using only the declared `files_matching` fields (`folder_segment`, `namespace_segment`, `file_name_suffix`, `file_name_prefix`) against `ArchitectureSourceFileFactIndex` facts, combining all populated fields with AND semantics, using exact/prefix/suffix string comparisons only — no regex or expression language.

#### Scenario: Folder segment selector matches files by directory component
- **WHEN** a contract declares `files_matching.folder_segment: Services`
- **THEN** the system SHALL select every source file whose `FolderSegments` contains `Services` and no others

#### Scenario: Namespace segment selector matches files by declared-type namespace component
- **WHEN** a contract declares `files_matching.namespace_segment: Services`
- **THEN** the system SHALL select every source file that declares at least one type whose `NamespaceSegments` contains `Services`

#### Scenario: File name suffix selector matches files by name
- **WHEN** a contract declares `files_matching.file_name_suffix: Service`
- **THEN** the system SHALL select every source file whose `FileNameWithoutExtension` ends with `Service`

#### Scenario: File name prefix selector matches files by name
- **WHEN** a contract declares `files_matching.file_name_prefix: I`
- **THEN** the system SHALL select every source file whose `FileNameWithoutExtension` starts with `I`

#### Scenario: Multiple selector fields combine with AND
- **WHEN** a contract declares both `files_matching.folder_segment: Services` and `files_matching.file_name_suffix: Service`
- **THEN** the system SHALL select only files that are both under a `Services` folder segment and whose file name ends with `Service`

#### Scenario: Files without source path data are never candidates
- **WHEN** a declared-type fact has a null `SourceFilePath` (ambiguous partial-class declaration or no source enrichment)
- **THEN** that fact's file SHALL NOT match any `folder_segment` or `file_name_suffix`/`file_name_prefix` selector field, regardless of the field's value

### Requirement: Evaluate type-kind expectations
The system SHALL allow `layout_conventions` contracts to require or forbid at least one declared type of a given `TypeKind` among a matched file's declared types via `require_type_kind` and `forbid_type_kind`.

#### Scenario: Matched file missing the required type kind is a violation
- **WHEN** a contract declares `require_type_kind: class` and a matched file declares no type with `TypeKind` equal to `Class`
- **THEN** strict validation SHALL return an architecture violation identifying the file, the expected type kind, and the actual declared type kinds in that file

#### Scenario: Matched file containing a forbidden type kind is a violation
- **WHEN** a contract declares `forbid_type_kind: interface` and a matched file declares a type with `TypeKind` equal to `Interface`
- **THEN** strict validation SHALL return an architecture violation identifying the file, the offending type, and the forbidden type kind

#### Scenario: Matched file satisfying type-kind expectations passes
- **WHEN** a contract declares `require_type_kind: class` and a matched file declares at least one type with `TypeKind` equal to `Class`
- **THEN** strict validation SHALL NOT report a type-kind violation for that file

### Requirement: Evaluate naming expectations on matched files' declared types
The system SHALL allow `layout_conventions` contracts to require or forbid a declared suffix/prefix on the simple name of every declared type in a matched file via `required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, and `forbidden_name_prefix`, using the same naming-check semantics as `type_placement_contracts`.

#### Scenario: Declared type missing a required suffix is a violation
- **WHEN** a contract declares `required_name_suffix: Service` and a matched file declares a type whose simple name does not end with `Service`
- **THEN** strict validation SHALL return an architecture violation identifying the type, its actual name, and the required suffix

#### Scenario: Declared type carrying a forbidden prefix is a violation
- **WHEN** a contract declares `forbidden_name_prefix: Impl` and a matched file declares a type whose simple name starts with `Impl`
- **THEN** strict validation SHALL return an architecture violation identifying the type, its actual name, and the forbidden prefix

### Requirement: Evaluate file-name-to-type-name correspondence
The system SHALL allow `layout_conventions` contracts to require that a matched file declares at least one type whose simple name equals the file's `FileNameWithoutExtension`, via `require_type_name_matches_file_name: true`.

#### Scenario: Matched file with no type matching the file name is a violation
- **WHEN** a contract declares `require_type_name_matches_file_name: true` and a matched file's declared types all have simple names different from its `FileNameWithoutExtension`
- **THEN** strict validation SHALL return an architecture violation identifying the file, its `FileNameWithoutExtension`, and the actual declared type names

#### Scenario: Matched file with a primary type matching the file name passes
- **WHEN** a contract declares `require_type_name_matches_file_name: true` and a matched file declares a type whose simple name equals its `FileNameWithoutExtension`
- **THEN** strict validation SHALL NOT report a file/type-name violation for that file

### Requirement: Evaluate matching-interface counterpart expectations
The system SHALL allow `layout_conventions` contracts to require that every matched concrete class have a corresponding interface declared somewhere in the analyzed source, via `require_matching_interface`, an object with an optional `name_prefix` field (defaulting to `I`). A concrete class satisfies the expectation when exactly one fact in `ArchitectureSourceFileFactIndex.AllFacts` has `SimpleTypeName` equal to `name_prefix` concatenated with the class's simple name and `TypeKind` equal to `Interface`.

#### Scenario: Matched class with no matching interface is a violation
- **WHEN** a contract declares `require_matching_interface: { name_prefix: I }`, a matched file declares class `OrderService`, and no fact has `SimpleTypeName` equal to `IOrderService` with `TypeKind` equal to `Interface`
- **THEN** strict validation SHALL return an architecture violation identifying `OrderService`, its source file, and the expected counterpart type name `IOrderService`

#### Scenario: Matched class with exactly one matching interface passes
- **WHEN** a contract declares `require_matching_interface: { name_prefix: I }`, a matched file declares class `OrderService`, and exactly one fact has `SimpleTypeName` equal to `IOrderService` with `TypeKind` equal to `Interface`
- **THEN** strict validation SHALL NOT report a missing-counterpart violation for `OrderService`

#### Scenario: Ambiguous counterpart candidates are treated as unresolved
- **WHEN** more than one fact has `SimpleTypeName` equal to the expected counterpart name with `TypeKind` equal to `Interface`
- **THEN** strict validation SHALL return an architecture violation identifying the ambiguity rather than selecting one candidate implicitly

#### Scenario: Default name prefix is "I" when unspecified
- **WHEN** a contract declares `require_matching_interface: {}` with no `name_prefix`
- **THEN** the system SHALL apply `I` as the expected counterpart prefix

### Requirement: Refine matched declared types with an optional CEL predicate
The system SHALL allow `files_matching.when` to compile as a boolean predicate under ArchLinter CEL Profile v1 against the existing `subject` context schema, evaluated once per declared type in a selector-matched file; a declared type for which `when` evaluates `false` (or fails to evaluate) SHALL be excluded from every expectation check for that contract.

#### Scenario: When predicate narrows matched declared types
- **WHEN** a contract declares `files_matching.folder_segment: Services` and `files_matching.when: subject.role == 'ApplicationService'`
- **THEN** only declared types in matched files whose resolved role equals `ApplicationService` SHALL be evaluated against the contract's expectations

#### Scenario: Invalid when expression fails policy loading
- **WHEN** a `files_matching.when` expression fails to compile under ArchLinter CEL Profile v1
- **THEN** policy loading SHALL fail with an actionable compilation diagnostic identifying the contract and the `when` source location

#### Scenario: When predicate compiles against the existing subject schema without new members
- **WHEN** a `files_matching.when` expression references `subject.sourcePaths`, `subject.sourceDirectoryPrefixes`, `subject.role`, `subject.kind`, or any other member of the closed `subject` shape
- **THEN** it compiles using the same `subject` `CelEnvironment` that `layers.<name>.selector.when` uses, with no schema members added for this contract family

### Requirement: Evaluate audit layout convention contracts
The system SHALL allow `contracts.audit_layout_conventions` entries to report layout violations without affecting strict validation.

#### Scenario: Audit layout convention violation is reported in audit mode
- **WHEN** an audit layout convention contract selects a file whose declared types fail an expectation
- **THEN** audit validation SHALL report an architecture violation for that file/type

#### Scenario: Audit layout convention violation does not fail strict validation
- **WHEN** a policy contains only an `audit_layout_conventions` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations
The system SHALL allow `ignored_violations` entries on a `layout_conventions` contract using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching layout violation
- **WHEN** a `layout_conventions` contract declares an `ignored_violations` entry matching a violating type or file
- **THEN** strict validation SHALL NOT report a violation for that match

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** a `layout_conventions` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic diagnostics identifying file, type, and expectation
The system SHALL emit, for each layout convention violation, a diagnostic identifying the matched source file, the contract name/id, and whichever of expected/actual type kind, expected/actual name, file/type-name mismatch, or expected/actual counterpart applied, in a stable, deterministic order.

#### Scenario: Diagnostic identifies expected and actual type kind
- **WHEN** a matched file fails a type-kind expectation
- **THEN** the emitted diagnostic SHALL include the file path, the expected type kind, and the actual declared type kinds

#### Scenario: Diagnostic identifies expected counterpart and requiring declaration
- **WHEN** a matched class fails a `require_matching_interface` expectation
- **THEN** the emitted diagnostic SHALL include the expected counterpart type name and the full name and source file of the class that required it

### Requirement: Unavailable source data produces a deterministic diagnostic instead of a silent pass
The system SHALL, when a `layout_conventions` contract is declared for a run whose `ArchitectureSourceFileFactIndex.AllFacts` contains zero facts with a non-null `SourceFilePath`, emit exactly one diagnostic per such contract explaining that path-based layout checks are unavailable for this run, instead of reporting zero violations.

#### Scenario: Contract declared with no source-enriched facts produces one diagnostic
- **WHEN** a `layout_conventions` contract is declared and the run's source fact index has no facts with a non-null `SourceFilePath` (e.g. `source_roots` is not configured)
- **THEN** validation SHALL emit exactly one diagnostic for that contract stating that path-based layout checks are unavailable, and SHALL NOT report an unrelated zero-violation pass as if the contract had been fully evaluated

#### Scenario: Contract declared with partial source enrichment evaluates normally
- **WHEN** a `layout_conventions` contract is declared and the run's source fact index has at least one fact with a non-null `SourceFilePath`
- **THEN** validation SHALL evaluate the contract normally against the available source-enriched facts, without emitting the unavailable-data diagnostic
