# attribute-role-extraction Specification

## Purpose
Extracts semantic architecture roles and metadata from type-level and assembly-level attributes,
per the `type_attribute`/`assembly_attribute` sources of the semantic-classification-model design.
Covers YAML binding for `classification.attributes`/`classification.assembly_attributes` and the
subset of `classification.precedence` that enables/disables these two sources, `CustomAttributeData`-based
extraction, the four fixed metadata-extraction forms, canonicalization into string/boolean/decimal,
type-over-assembly precedence, and deterministic conflict/evidence-extraction-failure handling.
## Requirements
### Requirement: classification.attributes and classification.assembly_attributes bind to typed configuration
`ArchitectureContractDocument` SHALL expose a `classification` section binding `classification.attributes` and `classification.assembly_attributes` as ordered lists of mapping entries, each with `attribute` (full type name string), `role` (string), and an optional `metadata` map of keys to either extraction-expression strings or literal scalar values (string/boolean/number). A policy declaring no `classification` section SHALL bind to an empty configuration and behave identically to a policy predating this capability.

#### Scenario: Policy without classification section is unaffected
- **WHEN** a policy document declares no `classification` section
- **THEN** the bound configuration has empty `attributes` and `assembly_attributes` lists and no role is ever assigned

#### Scenario: Mapping entries bind in declaration order
- **WHEN** a policy declares multiple entries under `classification.attributes`
- **THEN** the bound list preserves YAML declaration order, since declaration order is later used for same-tier conflict resolution

### Requirement: Type-level attributes are extracted and mapped to a role by full type name
For every scanned type, the extraction engine SHALL enumerate the type's own `CustomAttributeData` and match each attribute's full type name against every `classification.attributes` entry, without requiring the matched attribute type to belong to any ArchLinterNet-provided assembly.

#### Scenario: User-defined attribute assigns a role
- **WHEN** a `classification.attributes` entry declares `attribute: Acme.Architecture.DomainLayerAttribute`, `role: DomainLayer`, and a scanned type carries `[DomainLayerAttribute]`
- **THEN** the type's extraction result has `Role` equal to `DomainLayer`, regardless of which assembly defines `Acme.Architecture.DomainLayerAttribute`

#### Scenario: Type carrying no mapped attribute yields no type-attribute role
- **WHEN** a scanned type carries no attribute matching any `classification.attributes` entry
- **THEN** the type's extraction result has no role contributed by the `type_attribute` source

### Requirement: Assembly-level attributes are extracted and mapped to a role by full type name
For every scanned assembly, the extraction engine SHALL enumerate the assembly's own `CustomAttributeData` and match each attribute's full type name against every `classification.assembly_attributes` entry. The resolved assembly-level role/metadata SHALL apply to every type declared in that assembly as its `assembly_attribute` source contribution.

#### Scenario: Assembly-level attribute provides a default role for its types
- **WHEN** a `classification.assembly_attributes` entry declares `attribute: Acme.Architecture.BoundedContextAttribute`, `role: ApplicationLayer`, and a scanned assembly carries `[assembly: BoundedContextAttribute]`
- **THEN** every type declared in that assembly has an `assembly_attribute` source contribution of `role: ApplicationLayer`

### Requirement: Type-level attribute role overrides assembly-level attribute role
When both the `type_attribute` and `assembly_attribute` sources contribute a role/metadata assignment for one type, the extraction engine SHALL select the `type_attribute` source's assignment as the type's resolved role/metadata for these two sources.

#### Scenario: Type attribute wins over assembly attribute
- **WHEN** a type's declaring assembly contributes `role: ApplicationLayer` via `assembly_attribute` and the type itself contributes `role: DomainLayer` via `type_attribute`
- **THEN** the type's resolved role is `DomainLayer`

#### Scenario: Assembly attribute applies when no type attribute matches
- **WHEN** a type's declaring assembly contributes a role via `assembly_attribute` and the type itself carries no attribute matching any `classification.attributes` entry
- **THEN** the type's resolved role is the `assembly_attribute` source's role

### Requirement: classification.precedence disables the type_attribute or assembly_attribute source
When a policy declares `classification.precedence`, the extraction engine SHALL treat a source omitted from that list as disabled: an entry in `classification.attributes`/`classification.assembly_attributes` for a disabled source SHALL NOT contribute any role or metadata assignment. When `classification.precedence` is not declared, both sources are enabled, matching the fixed default order.

#### Scenario: Declared precedence without type_attribute disables it
- **WHEN** a policy declares `classification.precedence: [assembly_attribute]` and a type matches both a `classification.attributes` entry and a `classification.assembly_attributes` entry
- **THEN** the extraction engine assigns the `assembly_attribute` source's role, ignoring the `type_attribute` match entirely

#### Scenario: Declared precedence without either implemented source disables both
- **WHEN** a policy declares `classification.precedence` naming only unimplemented sources (e.g. `[namespace]`)
- **THEN** the extraction engine assigns no role from `classification.attributes` or `classification.assembly_attributes`, regardless of matching attributes

### Requirement: const: type references ambiguous across the type universe resolve as a failure
When a `const:<Full.Type.NAME>` reference's type-name portion matches more than one distinct `Type` in the extraction engine's type universe (e.g. the same full name compiled into two different scanned assemblies), the extraction engine SHALL NOT arbitrarily select one candidate; this resolves as an evidence-extraction failure identical to an unresolved type.

#### Scenario: Ambiguous const type name is an evidence-extraction failure
- **WHEN** two distinct types share the full name referenced by a `const:<Full.Type.NAME>` metadata expression
- **THEN** the referenced metadata key is omitted, the failure is recorded as a fact, and the role assignment from the matching source still proceeds

### Requirement: Conflict facts record which metadata differed, not only role names
A recorded conflict fact SHALL include a description of every metadata key whose value differed between the winning and discarded candidates, in addition to the winning and discarded role names. When two candidates share the same role and differ only in metadata, the fact SHALL still convey that difference rather than reporting an uninformative role-only comparison.

#### Scenario: Metadata-only conflict describes the differing key and values
- **WHEN** two candidates for one subject/source resolve to the same role but differing metadata for one key
- **THEN** the recorded conflict fact identifies that key and both competing values, not just the (identical) winning and discarded role names

#### Scenario: Distinct metadata-only conflicts on one subject are not merged
- **WHEN** more than one metadata-only conflict is recorded for the same subject, source, and role pair (e.g. from three or more repeated attribute instances), each with different competing metadata
- **THEN** every distinct conflict SHALL be preserved as a separate fact, including through any downstream deduplication (e.g. deduplicating recurring assembly-level facts across scanned types) — deduplication SHALL only collapse facts describing the exact same conflict

### Requirement: Same-tier attribute mapping conflicts resolve by YAML declaration order
When two or more entries within the same mapping list (`classification.attributes` or, separately, `classification.assembly_attributes`) match attributes present on one type or assembly with contradictory role/metadata, the extraction engine SHALL select the first-declared entry's role/metadata and SHALL record the discarded alternative as a conflict fact.

#### Scenario: Two attribute mapping entries matching one type resolve by declaration order
- **WHEN** a type carries two distinct attributes, each matched by a different `classification.attributes` entry with a different `role`
- **THEN** the extraction engine assigns the first-declared entry's role and records the discarded entry's role as a conflict fact

### Requirement: Repeated instances of one mapped attribute resolve by CustomAttributeData metadata order
When a repeatable attribute mapped by a single `classification.attributes`/`classification.assembly_attributes` entry appears more than once on one type or assembly, the extraction engine SHALL resolve the resulting conflict using the attribute instances' `CustomAttributeData` order (first instance wins), not YAML declaration order. Instances that resolve to the same role and the same metadata for every mapped key SHALL NOT be recorded as a conflict.

#### Scenario: Differing repeated instances resolve by first-in-metadata-order
- **WHEN** a type carries two instances of one attribute mapped by one `classification.attributes` entry, and the instances' extracted metadata differs
- **THEN** the extraction engine assigns the role/metadata of the first instance in `CustomAttributeData` order and records the discarded instance as a conflict fact

#### Scenario: Identical repeated instances are not a conflict
- **WHEN** a type carries two instances of one attribute mapped by one `classification.attributes` entry, and every instance resolves to the same role and the same metadata for every mapped key
- **THEN** the extraction engine assigns that role/metadata without recording a conflict fact

### Requirement: Metadata extraction supports four fixed forms
The extraction engine SHALL interpret each `metadata.<key>` value using exactly one of four forms, checked in this fixed order: `constructor[<index>]` (the attribute instance's zero-indexed, compiler-resolved positional constructor argument, including substituted default values), `property:<Name>` (a named argument explicitly present in the matched attribute usage's own `CustomAttributeData.NamedArguments`, never a type-declared default), `const:<Full.Type.NAME>` (a compile-time `const` field only, never `static readonly` or any other member), or a literal YAML scalar for any value matching none of the three reserved prefixes.

#### Scenario: Constructor-argument extraction including compiler-resolved defaults
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[0] }` and the matched attribute usage omits an optional constructor parameter with a default value
- **THEN** the extracted value is the fully compiler-resolved positional argument, including the substituted default

#### Scenario: Malformed constructor-index expression is an evidence-extraction failure, not a literal
- **WHEN** a `metadata.<key>` value starts with `constructor[` but is otherwise malformed (e.g. missing the closing `]`)
- **THEN** the extraction engine SHALL treat this as an attempted `constructor[<index>]` expression and resolve it as an evidence-extraction failure — it SHALL NOT fall through to the literal-scalar form merely because the expression is malformed

#### Scenario: property extraction ignores type-declared defaults
- **WHEN** a `classification.attributes` entry declares `metadata: { module: property:Module }` and the matched attribute usage does not explicitly supply `Module` as a named argument, even though the attribute type declares a settable `Module` property
- **THEN** the `module` key is treated as an evidence-extraction failure, not resolved from the property's declared default

#### Scenario: const extraction resolves only compile-time const fields
- **WHEN** a `const:<Full.Type.NAME>` reference names a `static readonly` field, an instance field, or any member whose value is not fixed at compile time
- **THEN** the extraction engine SHALL NOT execute, reflect over, or guess at the referenced member's runtime value, and this resolves as an evidence-extraction failure

#### Scenario: Literal scalar fallback
- **WHEN** a `metadata.<key>` value does not match `constructor[`, `property:`, or `const:`
- **THEN** the extraction engine uses the value verbatim as a literal YAML scalar

### Requirement: Extracted and literal metadata values canonicalize into string, boolean, or decimal
Every metadata value produced by extraction or supplied as a literal SHALL be canonicalized into exactly one of three domains before it is recorded: string (including `System.Type` canonicalized to `Type.FullName`, and enum values canonicalized to their declared member name only when the underlying value maps to exactly one declared member), boolean, or decimal (every CLR numeric primitive and every YAML/JSON numeric literal). A value with no representation in any of these three domains SHALL be treated as an evidence-extraction failure.

#### Scenario: Enum values canonicalize to their declared member name
- **WHEN** an extracted metadata value is an enum instance whose underlying value maps to exactly one declared member
- **THEN** the canonical value is that declared member's name, not its underlying numeric representation

#### Scenario: Enum canonicalization does not overflow for unsigned 64-bit underlying values
- **WHEN** an extracted metadata value is an enum instance with an unsigned 64-bit underlying type and a value exceeding `Int64.MaxValue` (e.g. `ulong.MaxValue`)
- **THEN** the extraction engine SHALL compare the value against declared members without a lossy numeric conversion and SHALL NOT throw

#### Scenario: Aliased enum values are an evidence-extraction failure
- **WHEN** an extracted metadata value is an enum instance whose underlying value maps to two or more declared members
- **THEN** the extraction engine SHALL NOT pick one declared member name arbitrarily; this resolves as an evidence-extraction failure

#### Scenario: System.Type values canonicalize to their full type name
- **WHEN** an extracted metadata value is a `System.Type` instance
- **THEN** the canonical value is `Type.FullName`

#### Scenario: Cross-representation numeric values compare equal
- **WHEN** one metadata value originates from a CLR numeric primitive and another originates from a YAML/JSON numeric literal representing the same number
- **THEN** both canonicalize to the same decimal value and compare equal

#### Scenario: High-precision YAML numeric literals do not lose precision before canonicalization
- **WHEN** a literal `metadata.<key>` value is a YAML numeric scalar with more significant digits than a CLR `double` can represent exactly, but within `decimal`'s range and precision
- **THEN** the canonical value SHALL equal the literal's exact decimal value — the literal SHALL NOT be parsed through `double` as an intermediate step when `decimal` can represent it exactly

### Requirement: Evidence-extraction failure never blocks role assignment
Whenever `constructor[<index>]`, `property:<Name>`, `const:<Full.Type.NAME>`, or value canonicalization fails for a given `metadata.<key>`, the extraction engine SHALL omit that key from the assigned metadata (not fabricate or default it), proceed with the role assignment from the matching source unaffected, and record the failure as a fact.

#### Scenario: Out-of-range constructor index omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[2] }` and the matched attribute instance's constructor invocation has fewer than 3 positional arguments
- **THEN** the `domain` key is omitted from the type's assigned metadata, the type still receives the entry's declared role, and the failure is recorded as a fact

#### Scenario: Missing named property omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { module: property:Module }` and the matched attribute instance has no named argument called `Module`
- **THEN** the `module` key is omitted from the type's assigned metadata, the type still receives the entry's declared role, and the failure is recorded as a fact

### Requirement: Conflict and evidence-extraction-failure facts are surfaced by the validate command
Recorded conflict and evidence-extraction-failure facts SHALL be exposed by the `validate` command's human, JSON, and CI-artifact output — deduplicated across every scanned type an assembly-level fact recurs for — without affecting `Passed`/exit-code pass/fail evaluation for any contract family.

#### Scenario: Human output includes classification findings when facts exist
- **WHEN** `validate` runs against a policy whose `classification.attributes`/`classification.assembly_attributes` produce at least one conflict or evidence-extraction-failure fact
- **THEN** the human-readable output includes a "Classification findings:" section listing each fact, and the command's pass/fail result is unaffected by their presence

#### Scenario: JSON output includes classification findings
- **WHEN** `validate --format json` runs against a policy producing classification facts
- **THEN** the JSON payload includes `classification_conflicts` and `classification_metadata_failures` arrays

#### Scenario: Assembly-level fact is not duplicated per type
- **WHEN** an assembly-level conflict or evidence-extraction-failure fact applies identically to every type declared in that assembly
- **THEN** the surfaced output lists that fact once, not once per type

### Requirement: Canonical context metadata is extracted and composed through attribute sources

The extraction engine SHALL recognize only the exact metadata-only context FQNs in the canonical annotation manifest. Type and assembly context evidence SHALL follow the corresponding enabled `type_attribute` and `assembly_attribute` sources, require one exact non-empty compile-time string constructor value, and require a supported canonical catalog identity. Context evidence SHALL not assign a role. For a resolved role, metadata SHALL be composed from winning role evidence first, then type context evidence for absent keys, then assembly context defaults for remaining absent keys. A type-scope key rejected because its context value is invalid or its values conflict SHALL remain absent from effective context metadata; an assembly default SHALL NOT repopulate that key. Winning role metadata SHALL retain its higher precedence. Custom YAML-mapped role metadata SHALL remain winning role evidence. Context evidence from a losing assembly role SHALL not be merged.

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

When canonical context evidence at one scope provides more than one distinct value for the same key, the extractor SHALL omit that key from the context contribution and emit a deterministic diagnostic identifying the key, values, and evidence. A rejected type-scope key SHALL also block assembly fallback for that key. The extractor SHALL retain an otherwise valid role, metadata from other keys, and higher-precedence metadata already supplied by winning role evidence. Equivalent duplicate values SHALL coalesce.

#### Scenario: Conflicting type context values omit only the affected key
- **WHEN** multiple type-scope canonical context evidence instances provide different values for one key and the type has a valid role
- **THEN** the key is omitted from the effective metadata, a deterministic diagnostic is recorded, and the role plus non-conflicting metadata remain

#### Scenario: Conflicting type context values block assembly fallback
- **WHEN** multiple type-scope canonical context evidence instances conflict for a key that also has an assembly default
- **THEN** the conflicting key remains absent from effective metadata and a deterministic conflict diagnostic is recorded

#### Scenario: Equivalent context values coalesce
- **WHEN** multiple context evidence instances at the same scope provide the same value for a key
- **THEN** the value appears once and no conflict diagnostic is emitted

#### Scenario: Invalid context values do not invent metadata
- **WHEN** a canonical context constructor value is missing, malformed, empty, or not a string
- **THEN** the key is omitted, an extraction failure and canonical diagnostic are recorded, and the role assignment proceeds

#### Scenario: Invalid type context values block assembly fallback
- **WHEN** a type-scope canonical context value is invalid for a key that also has an assembly default
- **THEN** the key remains absent from effective metadata, an extraction failure and canonical diagnostic are recorded, and the role assignment proceeds

