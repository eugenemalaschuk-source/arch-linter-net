## MODIFIED Requirements

### Requirement: Use referenced type metadata visible from project types
The system SHALL detect external dependency leakage through referenced type metadata available from project types through the current reference scanner path, including supported base types, interfaces, fields, properties, method parameters, return types, and generic arguments. The system SHALL ALSO detect external dependency references that appear exclusively inside method bodies by scanning IL bytecode for metadata token references and matching them against external dependency groups.

#### Scenario: Signature reference is detected
- **WHEN** a source type exposes a forbidden external type through a field, property, parameter, return type, base type, interface, or generic argument observed by the scanner
- **THEN** the external dependency contract SHALL evaluate that referenced type against forbidden external groups

#### Scenario: Method-body-only reference is detected
- **WHEN** a source type uses a forbidden external dependency only inside a method body (e.g., a method call, constructor call, or type reference in IL) and that usage is not visible through type-level metadata
- **THEN** the external dependency contract SHALL detect that reference and report an architecture violation

#### Scenario: Method-body reference includes member context
- **WHEN** a forbidden external dependency is found inside a method body
- **THEN** the violation SHALL identify the source type, the containing method or constructor name, the forbidden external group, and the referenced external member or type

#### Scenario: Method-body strict violation fails validation
- **WHEN** a strict external dependency contract contains a method-body-only violation
- **THEN** strict validation SHALL fail

#### Scenario: Method-body audit violation reports without failing strict
- **WHEN** an audit external dependency contract contains a method-body-only violation
- **THEN** audit validation SHALL report the violation and strict validation SHALL NOT fail

#### Scenario: Unresolved external metadata is not guaranteed
- **WHEN** an external assembly is unavailable or unresolved enough that referenced type metadata cannot be observed by the scanner
- **THEN** the external dependency contract SHALL NOT be required to report references from that unavailable metadata
