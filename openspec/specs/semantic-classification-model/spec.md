# semantic-classification-model Specification

## Purpose
TBD - created by archiving change design-semantic-classification-model. Update Purpose after archive.
## Requirements
### Requirement: Classification vocabulary is defined
The semantic classification model SHALL define exactly ten classification terms with non-overlapping meanings: `role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, and `uncovered semantic fact`.

#### Scenario: Uncovered semantic fact is distinguished from an unclassified type
- **WHEN** a type receives a role/metadata assignment from some classification source
- **AND** no `layers.<name>.selector` matches that role/metadata and no `override`/`exclusion` names the type
- **THEN** the model classifies this as an `uncovered semantic fact`, a status distinct from a type that received no role at all

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

### Requirement: Metadata extraction syntax is fixed and deterministic
Each `metadata.<key>` value SHALL be interpreted using exactly one of four forms, checked in this fixed order: `constructor[<index>]` (positional constructor argument), `property:<Name>` (named property/field), `const:<Full.Type.NAME>` (compile-time `const` field only), or a literal YAML scalar for any value matching none of the three reserved prefixes. No regex or reflection-expression syntax beyond these four forms SHALL be introduced.

#### Scenario: Constructor-argument extraction
- **WHEN** a `classification.attributes` entry declares `metadata: { domain: constructor[0] }`
- **THEN** the reviewed design specifies the value as the attribute's zero-indexed positional constructor argument

#### Scenario: Literal scalar fallback
- **WHEN** a `metadata.<key>` value does not match `constructor[`, `property:`, or `const:`
- **THEN** the reviewed design specifies the value is used verbatim as a literal YAML scalar

#### Scenario: const resolves only compile-time const fields, never static readonly
- **WHEN** a `const:<Full.Type.NAME>` reference names a `static readonly` field, an instance field, a computed member, or any member whose value is not fixed at compile time
- **THEN** the reviewed design classifies this as a deterministic unresolved-constant condition; extraction SHALL NOT execute, reflect over, or guess at the referenced member's runtime value, and SHALL report the failure explicitly rather than silently omitting the metadata key

### Requirement: Selector syntax is additive to the existing layer shape
`layers.<name>.selector` SHALL be a new optional field on the existing `layer` schema shape, sibling to `namespace`/`namespace_suffix`/`external`, requiring `role` and allowing an optional exact-match `metadata` object. A layer SHALL be permitted to declare `namespace`, `selector`, or both; `namespace`'s existing required-ness SHALL be relaxed to an alternative (`namespace` OR `selector`) without changing `namespace`'s own matching semantics.

#### Scenario: Selector-only layer is schema-valid
- **WHEN** a layer declares `selector` with a `role` and no `namespace`
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
The design SHALL state that a future `scope: semantic_role` variant of the existing architecture-coverage-model contract (`covered`/`excluded`/`uncovered`/`unknown`/`stale`/`empty-input`, per the `architecture-coverage-model` capability) is the intended integration point for classification's `uncovered semantic fact` and `stale selector` concepts, rather than introducing a separate coverage-like diagnostic vocabulary.

#### Scenario: Uncovered semantic fact aligns with coverage's uncovered term
- **WHEN** a role is discovered by classification but matched by no selector and named by no override/exclusion
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

