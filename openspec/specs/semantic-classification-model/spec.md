# semantic-classification-model Specification

## Purpose
Define the semantic-classification vocabulary, the reserved `classification` YAML section, and the `layers.<name>.selector` shape that future role-discovery/extraction work (#108-#114) implements against. This capability is design-only: no attribute/inheritance/namespace/path extraction, role assignment, selector matching, or runtime binding is implemented by it.
## Requirements
### Requirement: Classification vocabulary is defined
The semantic classification model SHALL define exactly ten classification terms with non-overlapping meanings: `role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, and `uncovered semantic fact`.

#### Scenario: Uncovered semantic fact is distinguished from an unclassified type
- **WHEN** a type receives a role/metadata assignment from some classification source
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model classifies this as an `uncovered semantic fact`, a status distinct from a type that received no role at all

#### Scenario: An override-assigned role does not by itself exempt a type from coverage
- **WHEN** a type's role/metadata was assigned by a `classification.overrides` entry
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model SHALL still classify this as an `uncovered semantic fact` — `override` is a classification source, not a coverage-exemption mechanism, and only `exclusion` removes a type from coverage consideration

#### Scenario: Consumption is not hard-coded to layers.<name>.selector alone
- **WHEN** #111/#112 introduce a contextual contract construct that references a discovered role/metadata value directly, without that role/metadata also being matched by a `layers.<name>.selector`
- **THEN** the reviewed design requires that construct to count as a coverage-participating consumer identically to a selector match — a type consumed only by such a direct reference SHALL NOT be classified as an `uncovered semantic fact`

#### Scenario: Stale selector is distinguished from an unmatched namespace layer
- **WHEN** a `layers.<name>.selector`'s `role`/`metadata` criteria match zero types classified by the model
- **THEN** the model classifies this as a `stale selector`, a status distinct from any existing namespace-layer diagnostic

### Requirement: Classification is a top-level configuration section, not a contract family
The reviewed schema SHALL define a top-level `classification` object, structurally independent of `contracts.strict_*`/`contracts.audit_*` families, since classification produces facts consumed by other constructs rather than pass/fail validation itself.

#### Scenario: Classification section exists independently of contract families
- **WHEN** a policy declares a `classification` section
- **THEN** it is validated as a distinct top-level section and does not alter the behavior of `strict`, `strict_layers`, `strict_coverage`, or any other existing contract family

### Requirement: Source precedence is fixed and only narrowable
The reviewed schema SHALL fix the classification source precedence order as `yaml_override > type_attribute > assembly_attribute > inheritance > namespace > path`. An optional `classification.precedence` field SHALL accept only a non-empty subsequence of this fixed order, with no repeated entries; sources it omits are disabled for that policy. Omitting `classification.precedence` entirely SHALL default to all six sources in the fixed order. This SHALL be enforced structurally by the schema itself (not documented only), by enumerating every valid ordered, duplicate-free subsequence as an explicit `oneOf` of `const` values — an item-level `enum` restricting individual tokens is insufficient, since it does not by itself reject a reordered or duplicated list.

#### Scenario: Declared precedence disables omitted sources
- **WHEN** a policy declares `classification.precedence: [yaml_override, namespace]`
- **THEN** `type_attribute`, `assembly_attribute`, `inheritance`, and `path` sources SHALL NOT contribute any role or metadata assignment for that policy

#### Scenario: Precedence cannot reorder the fixed tiers
- **WHEN** a policy declares `classification.precedence` with entries out of the fixed relative order (e.g. `namespace` before `type_attribute`)
- **THEN** the reviewed schema SHALL reject the document as invalid, since precedence expresses a subsequence of the fixed order, not an arbitrary permutation

#### Scenario: Precedence cannot repeat a source
- **WHEN** a policy declares `classification.precedence` with a repeated entry (e.g. `[namespace, namespace]`)
- **THEN** the reviewed schema SHALL reject the document as invalid

#### Scenario: Precedence cannot be declared empty
- **WHEN** a policy declares `classification.precedence: []`
- **THEN** the reviewed schema SHALL reject the document as invalid; an author who wants to disable every source omits `precedence` together with every source's own entries instead

### Requirement: Type and assembly attribute mapping requires no binary annotation dependency
`classification.attributes` and `classification.assembly_attributes` entries SHALL map by full attribute type name (`attribute: <FullTypeName>`) against attributes declared or referenced in scanned code, without requiring any ArchLinterNet-provided binary annotation assembly.

#### Scenario: User-defined attribute is mappable by full type name
- **WHEN** a `classification.attributes` entry declares `attribute: Acme.Architecture.DomainLayerAttribute`
- **THEN** the model maps that attribute to the declared `role`/`metadata` regardless of which assembly defines `Acme.Architecture.DomainLayerAttribute`

### Requirement: Same-tier conflicts across mapping entries resolve by YAML declaration order, for every source
When two or more entries *within the same source list* (`classification.attributes`, `classification.assembly_attributes`, `classification.inheritance`, `classification.namespace`, `classification.path`, or `classification.overrides`) match one type with contradictory role/metadata, the model SHALL deterministically select the *first-declared entry's* role/metadata and SHALL record the discarded alternative as a `conflict` fact. This rule applies uniformly across all six sources and is distinct from precedence (Decision covering source precedence), which resolves conflicts *between* sources, not within one.

#### Scenario: Two namespace mapping entries matching one namespace resolve by declaration order
- **WHEN** two `classification.namespace` entries both match the same namespace and assign different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

#### Scenario: Two override entries matching one type resolve by declaration order
- **WHEN** a `classification.overrides` entry naming a type directly (`type: MyApp.Order`) and a separate broad `classification.overrides` entry (`namespace: MyApp`) both match the same type with different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

#### Scenario: Within-source conflict resolution does not override source precedence
- **WHEN** a `classification.namespace` entry and a `classification.overrides` entry both match one type
- **THEN** the fixed source precedence (`yaml_override` before `namespace`) decides the winner, not declaration order — declaration-order tie-breaking applies only among entries within the same source

### Requirement: Repeated instances of one mapped attribute resolve by metadata order, not YAML order
When a repeatable attribute mapped by a single `classification.attributes`/`classification.assembly_attributes` entry appears more than once on one declaration, the model SHALL resolve the resulting conflict using the attribute instances' `CustomAttributeData` metadata order (first instance wins), not YAML declaration order (there is only one mapping entry to order). Identical instances (same role and same metadata for every key the entry maps) SHALL NOT be treated as a conflict.

#### Scenario: Differing repeated instances resolve by first-in-metadata-order
- **WHEN** a type carries two instances of the same attribute mapped by one `classification.attributes` entry, and the instances' extracted metadata differs
- **THEN** the model assigns the role/metadata of the first instance in `CustomAttributeData` metadata order and records the discarded instance as a `conflict` fact

#### Scenario: Identical repeated instances are not a conflict
- **WHEN** a type carries two instances of the same attribute mapped by one `classification.attributes` entry, and every instance resolves to the same role and the same metadata for every mapped key
- **THEN** the model assigns that role/metadata without recording a `conflict` fact

### Requirement: Metadata extraction syntax is fixed and deterministic
Each `metadata.<key>` value SHALL be interpreted using exactly one of four forms, checked in this fixed order: `constructor[<index>]` (positional constructor argument), `property:<Name>` (named property/field), `const:<Full.Type.NAME>` (compile-time `const` field only), or a literal YAML scalar for any value matching none of the three reserved prefixes. No regex or reflection-expression syntax beyond these four forms SHALL be introduced.

#### Scenario: Constructor-argument extraction
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[0] }`
- **THEN** the reviewed design specifies the value as the attribute's zero-indexed positional constructor argument

#### Scenario: Constructor-argument indexing includes compiler-resolved defaults
- **WHEN** a matched attribute's constructor invocation omits an optional parameter that has a default value (e.g. `[Domain("Sales")]` against `Domain(string name, string module = "Unknown")`)
- **THEN** `constructor[<index>]` indexes the fully compiler-resolved argument list, including the substituted default value, not only the arguments visually written at the call site

#### Scenario: Literal scalar fallback
- **WHEN** a `metadata.<key>` value does not match `constructor[`, `property:`, or `const:`
- **THEN** the reviewed design specifies the value is used verbatim as a literal YAML scalar

#### Scenario: const resolves only compile-time const fields, never static readonly
- **WHEN** a `const:<Full.Type.NAME>` reference names a `static readonly` field, an instance field, a computed member, or any member whose value is not fixed at compile time
- **THEN** the reviewed design classifies this as a deterministic unresolved-constant condition; extraction SHALL NOT execute, reflect over, or guess at the referenced member's runtime value

### Requirement: property:Name reads only explicitly supplied named arguments, never a type-declared default
`property:<Name>` SHALL resolve successfully only when `<Name>` is a named argument explicitly present in the matched attribute usage's own recorded metadata (e.g. `System.Reflection.CustomAttributeData.NamedArguments`) — never merely because the attribute *type* declares a settable property or field named `<Name>`. A property/field that exists on the attribute type but was not supplied in that specific usage SHALL be treated identically to a property/field that does not exist at all.

#### Scenario: Unsupplied-but-existing property is a failure identical to a nonexistent property
- **WHEN** a `classification.attributes` entry declares `metadata: { module: property:Module }`, the matched attribute type declares a settable `Module` property, and the specific attribute usage (e.g. `[Domain("Sales")]`) does not explicitly supply `Module` as a named argument
- **THEN** extraction SHALL NOT instantiate the attribute or read `Module`'s declared default/initializer value, and this resolves as the same evidence-extraction failure as a nonexistent property

### Requirement: Metadata values are canonicalized into a fixed set of comparable domains
Every metadata value — extracted or literal — SHALL be canonicalized into exactly one of three domains before comparison: **string** (CLR `string`; `System.Type` canonicalized to `Type.FullName`; enum values canonicalized to their declared member name, but only when the underlying value maps to exactly one declared member — an ambiguous/aliased underlying value is an evidence-extraction failure, never a guessed name), **boolean**, or **decimal** (every CLR numeric primitive — `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, and `decimal` itself — and every YAML/JSON numeric literal canonicalized to `decimal`). A value with no representation in any of these three domains SHALL be treated as an evidence-extraction failure.

#### Scenario: Enum values canonicalize to their declared member name, not the underlying integer
- **WHEN** an extracted metadata value is an enum instance whose underlying value maps to exactly one declared member
- **THEN** the canonical value SHALL be that declared member's name (a string), not its underlying numeric representation

#### Scenario: Aliased enum values are an evidence-extraction failure, never a guessed name
- **WHEN** an extracted metadata value is an enum instance whose underlying value maps to two or more declared members (an aliased value, e.g. `enum Tier { Core = 1, Domain = 1 }`)
- **THEN** extraction SHALL NOT pick one declared member name arbitrarily; this resolves as the same evidence-extraction failure as an unmapped enum value

#### Scenario: System.Type values canonicalize to their full type name
- **WHEN** an extracted metadata value is a `System.Type` instance
- **THEN** the canonical value SHALL be `Type.FullName` (a string)

#### Scenario: Cross-representation numeric values compare equal
- **WHEN** one metadata value originates from a CLR numeric primitive (e.g. `int` `1`) and another originates from a YAML/JSON numeric literal (e.g. `1.0`)
- **THEN** both SHALL canonicalize to the same `decimal` value and SHALL compare equal

#### Scenario: const decimal resolves through the canonical numeric domain
- **WHEN** a `const:<Full.Type.NAME>` reference resolves to a compile-time `const decimal` field
- **THEN** the value SHALL canonicalize into the `decimal` domain directly (it is already that domain's own CLR representation) and SHALL compare equal to any other value that canonicalizes to the same `decimal` number

#### Scenario: Unsupported value shapes are an evidence-extraction failure
- **WHEN** an extracted metadata value is an array, another attribute-typed value, `null`, an unmapped enum value, or a number with no `decimal` representation (e.g. `NaN`, `Infinity`)
- **THEN** extraction SHALL NOT attempt to serialize or flatten it; this resolves as the same evidence-extraction failure as any other unresolvable metadata reference

### Requirement: Evidence-extraction failure is uniform across forms and never blocks role assignment
Each of `constructor[<index>]`, `property:<Name>`, and `const:<Full.Type.NAME>` — including every canonical-value-domain failure above — SHALL resolve failure identically: the failed metadata key SHALL be omitted from the assigned metadata (not fabricated, not defaulted), the role assignment from the matching source SHALL still proceed unaffected, and the failure SHALL be recorded as an explainable fact for diagnostics.

#### Scenario: Out-of-range constructor index omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[2] }` and the matched attribute instance's constructor invocation has fewer than 3 positional arguments
- **THEN** the `domain` key is omitted from that type's assigned metadata, the type still receives the entry's declared `role`, and the failure is recorded as an explainable fact

#### Scenario: Missing named property omits the key without blocking the role
- **WHEN** a `classification.attributes` entry declares `metadata: { module: property:Module }` and the matched attribute instance has no public property or field named `Module`
- **THEN** the `module` key is omitted from that type's assigned metadata, the type still receives the entry's declared `role`, and the failure is recorded as an explainable fact

#### Scenario: Unresolved const reference omits the key without blocking the role
- **WHEN** a `const:<Full.Type.NAME>` reference does not resolve to a compile-time `const` field
- **THEN** that metadata key is omitted from the type's assigned metadata, the type still receives the entry's declared `role`, and the failure is recorded as an explainable fact

### Requirement: Selector syntax is additive to the existing layer shape, and namespace remains required
`layers.<name>.selector` SHALL be a new optional field on the existing `layer` schema shape, sibling to `namespace`/`namespace_suffix`/`external`, requiring `role` and allowing an optional exact-match `metadata` object. `namespace` SHALL remain a required field on every layer; `selector` is additive alongside it, never a substitute for it — a selector-only layer (no `namespace`) is schema-invalid, because `ArchitectureLayerResolver.IsProjectType` iterates every declared layer unconditionally and evaluates `layer.GlobPattern`, which throws for an empty `Namespace` at real execution time, not only at schema-validation or YAML-load time.

#### Scenario: Selector-only layer is rejected
- **WHEN** a layer declares `selector` with a `role` and no `namespace`
- **THEN** the reviewed schema SHALL reject the layer definition as invalid

#### Scenario: A layer may declare namespace and selector together
- **WHEN** a layer declares both `namespace` and `selector`
- **THEN** the reviewed schema SHALL accept the layer definition as valid

#### Scenario: Existing namespace-only layers are unaffected
- **WHEN** a layer declares only `namespace` (no `selector`), as every layer did before this change
- **THEN** the reviewed schema SHALL continue to accept and interpret it identically to its pre-change behavior

#### Scenario: Selector metadata matching is exact and AND-combined
- **WHEN** a `layers.<name>.selector` declares `metadata` with more than one key
- **THEN** the reviewed design requires every declared key to match exactly for a type to be selected — no wildcard or regex value matching is introduced

### Requirement: Overrides require a reason only when broad
A `classification.overrides` entry scoped to a single `type` SHALL NOT require `reason`. A `classification.overrides` entry scoped to a `namespace` or `namespace_suffix` SHALL require a non-empty `reason`.

#### Scenario: Type-scoped override without reason is valid
- **WHEN** a `classification.overrides` entry declares `type: <FullTypeName>` and omits `reason`
- **THEN** the reviewed schema SHALL accept the entry as valid

#### Scenario: Namespace-scoped override without reason is rejected
- **WHEN** a `classification.overrides` entry declares `namespace` or `namespace_suffix` and omits `reason`
- **THEN** the reviewed schema SHALL reject the document as invalid

### Requirement: Override and exclusion scopes are mutually exclusive
Every `classification.overrides`/`classification.exclusions` entry SHALL declare exactly one of `type`, `namespace`, or `namespace_suffix`. An entry declaring more than one of these fields SHALL be rejected by the reviewed schema, regardless of whether `reason` is present, so that a broad-scope field cannot ride alongside a narrow-scope field to bypass the narrow scope's optional-`reason` allowance.

#### Scenario: Combining a narrow and broad scope on one override is rejected
- **WHEN** a `classification.overrides` entry declares both `type` and `namespace` (with or without `reason`)
- **THEN** the reviewed schema SHALL reject the document as invalid, since scopes are mutually exclusive and this design defines no combined-scope semantics

#### Scenario: Combining a narrow and broad scope on one exclusion is rejected
- **WHEN** a `classification.exclusions` entry declares more than one of `type`, `namespace`, or `namespace_suffix`
- **THEN** the reviewed schema SHALL reject the document as invalid

### Requirement: Exclusions always require a reason
Every `classification.exclusions` entry SHALL require a non-empty `reason` field, regardless of scope.

#### Scenario: Exclusion without reason is rejected
- **WHEN** a `classification.exclusions` entry omits `reason`
- **THEN** the reviewed schema SHALL reject the document as invalid

### Requirement: Classification interacts with coverage through an aligned, not parallel, vocabulary
The design SHALL state that a future `scope: semantic_role` variant of the existing architecture-coverage-model contract (`covered`/`excluded`/`uncovered`/`unknown`/`stale`/`empty-input`, per the `architecture-coverage-model` capability) is the intended integration point for classification's `uncovered semantic fact` and `stale selector` concepts, rather than introducing a separate coverage-like diagnostic vocabulary. "Consumed" for this purpose SHALL NOT be hard-coded to `layers.<name>.selector` alone: any future contextual-contract construct (#111/#112) that references a discovered role/metadata value directly SHALL count as coverage-participating consumption identically to a selector match.

#### Scenario: Uncovered semantic fact aligns with coverage's uncovered term
- **WHEN** a role is discovered by classification but consumed by no coverage-participating construct and named by no exclusion (including a role assigned by an `override`, which does not by itself exempt a type from coverage)
- **THEN** the reviewed design classifies this using the same conceptual status as the architecture-coverage-model's `uncovered`, for a future `scope: semantic_role` coverage variant to implement

### Requirement: No runtime behavior is introduced by this design
This change SHALL NOT add any C# binding for `classification`/`selector`, any extraction logic, any role-assignment logic, or any load-time guard rejecting policies that declare `classification`/`selector`. A policy declaring `classification` or `layers.<name>.selector` before those bindings exist SHALL be schema-valid but produce no classification behavior.

#### Scenario: Declaring classification before the engine exists does not throw
- **WHEN** a policy declares a `classification` section or a `layers.<name>.selector` field before any implementation task lands
- **THEN** policy loading and validation SHALL proceed exactly as if the section/field were absent, with no exception thrown and no role ever assigned

### Requirement: Existing policies remain unaffected
A policy with no `classification` section and no `layers.<name>.selector` field SHALL behave identically to its behavior before the classification model existed.

#### Scenario: Policy without classification is unaffected
- **WHEN** a policy declares no `classification` section and no layer uses `selector`
- **THEN** no classification-related schema field constrains that policy beyond what already applied before this change

