## ADDED Requirements

### Requirement: Canonical context metadata is extracted and composed through attribute sources

The extraction engine SHALL recognize only the exact metadata-only context FQNs in the canonical annotation manifest. Type and assembly context evidence SHALL follow the corresponding enabled `type_attribute` and `assembly_attribute` sources, require one exact non-empty compile-time string constructor value, and require a supported canonical catalog identity. Context evidence SHALL not assign a role. For a resolved role, metadata SHALL be composed from winning role evidence first, then type context evidence for absent keys, then assembly context defaults for remaining absent keys. Custom YAML-mapped role metadata SHALL remain winning role evidence. Context evidence from a losing assembly role SHALL not be merged.

#### Scenario: Assembly context enriches a type role
- **WHEN** a type has a canonical role and its assembly has a supported canonical identity plus a context-only assembly attribute
- **THEN** the role keeps its identity and metadata, and the assembly context supplies a missing metadata key

#### Scenario: Type context wins over an assembly default
- **WHEN** type and assembly context attributes provide different values for the same key not supplied by winning role evidence
- **THEN** the type value is selected regardless of reflection enumeration order

#### Scenario: Custom YAML role metadata remains authoritative
- **WHEN** a custom-mapped role attribute supplies metadata for a key and a canonical context attribute supplies a different value for the same key
- **THEN** the mapped role metadata remains effective and the canonical context attribute does not replace it

#### Scenario: Context metadata respects source enablement
- **WHEN** `classification.precedence` disables `type_attribute` or `assembly_attribute`
- **THEN** context attributes at that disabled scope contribute neither metadata nor diagnostics

### Requirement: Same-scope context metadata conflicts fail closed per key

When canonical context evidence at one scope provides more than one distinct value for the same key, the extractor SHALL omit that key from the context contribution and emit a deterministic diagnostic identifying the key, values, and evidence. It SHALL retain an otherwise valid role, metadata from other keys, and higher-precedence metadata already supplied by winning role evidence. Equivalent duplicate values SHALL coalesce.

#### Scenario: Conflicting type context values omit only the affected key
- **WHEN** multiple type-scope canonical context evidence instances provide different values for one key and the type has a valid role
- **THEN** the key is omitted from the effective metadata, a deterministic diagnostic is recorded, and the role plus non-conflicting metadata remain

#### Scenario: Equivalent context values coalesce
- **WHEN** multiple context evidence instances at the same scope provide the same value for a key
- **THEN** the value appears once and no conflict diagnostic is emitted

#### Scenario: Invalid context values do not invent metadata
- **WHEN** a canonical context constructor value is missing, malformed, empty, or not a string
- **THEN** the key is omitted, an extraction failure and canonical diagnostic are recorded, and the role assignment proceeds
