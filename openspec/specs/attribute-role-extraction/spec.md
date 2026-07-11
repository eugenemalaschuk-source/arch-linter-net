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

### Requirement: Evidence-extraction failure never blocks role assignment
Whenever `constructor[<index>]`, `property:<Name>`, `const:<Full.Type.NAME>`, or value canonicalization fails for a given `metadata.<key>`, the extraction engine SHALL omit that key from the assigned metadata (not fabricate or default it), proceed with the role assignment from the matching source unaffected, and record the failure as a fact.

#### Scenario: Out-of-range constructor index omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[2] }` and the matched attribute instance's constructor invocation has fewer than 3 positional arguments
- **THEN** the `domain` key is omitted from the type's assigned metadata, the type still receives the entry's declared role, and the failure is recorded as a fact

#### Scenario: Missing named property omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { module: property:Module }` and the matched attribute instance has no named argument called `Module`
- **THEN** the `module` key is omitted from the type's assigned metadata, the type still receives the entry's declared role, and the failure is recorded as a fact

