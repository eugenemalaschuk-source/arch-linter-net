## ADDED Requirements

### Requirement: Detect forbidden calls via Roslyn semantic analysis
The system SHALL parse C# source files, build a Roslyn compilation, walk executable bodies (methods, constructors, accessors, local functions), resolve symbols via `SemanticModel`, and match against forbidden call patterns.

#### Scenario: Forbidden call found in method body
- **WHEN** a method body contains a call matching a forbidden pattern
- **THEN** a violation is returned with the file path, line number, matched pattern, and resolved symbol

#### Scenario: No forbidden calls
- **WHEN** no method bodies contain calls matching forbidden patterns
- **THEN** the contract returns an empty violation list

#### Scenario: Source file not in target namespace
- **WHEN** a source file does not contain types in the source namespace prefix
- **THEN** that file is skipped entirely

### Requirement: Detect forbidden calls via IL token fallback
The system SHALL read raw IL byte arrays from compiled assemblies, decode opcodes, resolve metadata tokens to `MemberInfo`, and match against forbidden call patterns.

#### Scenario: IL-level forbidden call found
- **WHEN** a method's IL contains a call token matching a forbidden pattern
- **THEN** a violation is returned with the IL offset, method name, matched pattern, and resolved member

#### Scenario: Missing assembly gracefully handled
- **WHEN** a method body cannot be read due to `FileNotFoundException`
- **THEN** that method is skipped without error

### Requirement: Merge duplicate semantic/IL matches
The system SHALL merge violations from Roslyn and IL scanning by normalized descriptor to reduce duplicate findings.

#### Scenario: Same call found by both scanners
- **WHEN** both Roslyn and IL scanning find the same forbidden call
- **THEN** only one violation entry is produced for that call
