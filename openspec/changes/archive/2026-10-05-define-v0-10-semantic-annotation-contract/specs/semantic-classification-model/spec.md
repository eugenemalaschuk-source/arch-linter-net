## MODIFIED Requirements

### Requirement: Same-tier conflicts across mapping entries resolve by YAML declaration order, for every source

Conflicts between legacy user-owned YAML mapping entries within the same source list (`classification.attributes`, `classification.assembly_attributes`, `classification.inheritance`, `classification.namespace`, `classification.path`, or `classification.overrides`) SHALL preserve the documented first-declared resolution and SHALL record discarded alternatives as conflict facts. Canonical role evidence SHALL NOT be resolved by declaration or metadata enumeration order: equivalent canonical/custom evidence coalesces with provenance, while contradictory roles or conflicting same-specificity role metadata produce a deterministic conflict and no effective role for the affected type. This rule is distinct from source precedence, which continues to select the higher-precedence role source.

#### Scenario: Two namespace mapping entries matching one namespace resolve by declaration order

- **WHEN** two `classification.namespace` entries both match the same namespace and assign different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

#### Scenario: Two override entries matching one type resolve by declaration order

- **WHEN** a `classification.overrides` entry naming a type directly (`type: MyApp.Order`) and a separate broad `classification.overrides` entry (`namespace: MyApp`) both match the same type with different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

#### Scenario: Within-source conflict resolution does not override source precedence

- **WHEN** a `classification.namespace` entry and a `classification.overrides` entry both match one type
- **THEN** the fixed source precedence (`yaml_override` before `namespace`) decides the winner, not declaration order — declaration-order tie-breaking applies only among entries within the same source

#### Scenario: Two inheritance mapping entries matching one type via different base types resolve by declaration order

- **WHEN** two `classification.inheritance` entries both match one type (e.g. one matching a base class, another matching an interface the type also implements) and assign different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

#### Scenario: Conflicting canonical roles on one type fail closed

- **WHEN** two canonical role annotations on one type resolve to different roles
- **THEN** the model records a deterministic conflict and exposes no effective role for that type

#### Scenario: Equivalent canonical and custom evidence coalesces

- **WHEN** canonical annotation evidence and a distinct user-owned mapped attribute resolve to the same role and equivalent role metadata at the same source specificity
- **THEN** the model exposes one effective role, retains both provenance sources, and records no role conflict

#### Scenario: Conflicting canonical and custom evidence does not use ordering

- **WHEN** canonical annotation evidence and a user-owned mapped attribute resolve to different roles or conflicting same-specificity role metadata
- **THEN** the model records a deterministic conflict and exposes no effective role for that type, regardless of attribute enumeration or YAML order

#### Scenario: Type-level role evidence outranks assembly-level role evidence

- **WHEN** a type-level canonical role conflicts with an assembly-level role annotation
- **THEN** the type-level role wins under the existing fixed source precedence and assembly role metadata is not merged into the winning role

## REMOVED Requirements

### Requirement: Type and assembly attribute mapping requires no binary annotation dependency

**Reason**: Issue #566 approves an optional source-only annotation package for the v0.10 capability wave while retaining user-owned mappings as a supported adoption path.

**Migration**: Existing policies may continue mapping user-owned attributes by exact full type name. New annotation-aware tools recognize the reserved canonical FQNs directly, and YAML cannot redefine those identities.

## ADDED Requirements

### Requirement: Canonical source annotations share the existing classification pipeline

`classification.attributes` and `classification.assembly_attributes` entries SHALL map user-owned attributes by exact full attribute type name (`attribute: <FullTypeName>`) regardless of the defining assembly. The optional source-only v0.10 package SHALL also be recognized by exact canonical attribute FQN through the same classification pipeline, without a per-annotation YAML mapping or a separate runtime annotation assembly. User-owned attribute mappings SHALL remain first-class. YAML SHALL NOT remap a reserved canonical FQN to a different role or metadata contract.

#### Scenario: User-defined attribute is mappable by full type name

- **WHEN** a `classification.attributes` entry declares `attribute: Acme.Architecture.DomainLayerAttribute`
- **THEN** the model maps that attribute to the declared `role`/`metadata` regardless of which assembly defines `Acme.Architecture.DomainLayerAttribute`

#### Scenario: Canonical annotation is recognized without a YAML mapping

- **WHEN** a scanned type carries an exact v0.10 canonical annotation FQN
- **THEN** the existing attribute evidence, classification resolution, role index, selectors, contracts, coverage, and explain surfaces expose the mapped role without requiring a `classification.attributes` entry

#### Scenario: A canonical FQN cannot be remapped

- **WHEN** a policy attempts to map a reserved canonical annotation FQN to a different role or metadata contract
- **THEN** policy validation reports a deterministic configuration diagnostic and does not reinterpret the canonical identity

#### Scenario: Canonical annotations do not require a runtime annotation assembly

- **WHEN** a project compiles source definitions from the optional annotation package
- **THEN** Core recognizes the resulting metadata names without requiring the linter process to load an `ArchLinterNet.Annotations` assembly

### Requirement: Bounded canonical context metadata composes deterministically with one primary role

Canonical role annotations MAY carry the six fixed metadata keys `domain`, `boundedContext`, `module`, `feature`, `platform`, and `runtime`. Matching metadata-only canonical attributes MAY supply those same keys at type or assembly scope. Supplied values SHALL be exact, non-empty compile-time strings with no normalization or name inference. An empty value SHALL omit the affected key, retain an otherwise valid role, and produce a metadata-extraction failure. Metadata composition SHALL use the following order: metadata from the winning role evidence, then type-scope metadata-only evidence, then assembly-scope metadata-only defaults for keys still absent. Equal values coalesce and retain provenance. Conflicting values at the same specificity SHALL produce a deterministic metadata conflict and omit only that key while preserving the resolved role. Type-scope metadata overrides assembly defaults without conflict. Lower-precedence role metadata SHALL NOT fill missing keys on a higher-precedence role. No arbitrary first-party metadata key or property bag SHALL be introduced; custom YAML mappings remain available for other metadata.

#### Scenario: Role annotation supplies bounded context metadata

- **WHEN** a canonical role annotation declares one or more of the six supported metadata properties
- **THEN** the classifier attaches those values to the role fact through the existing role index

#### Scenario: Empty canonical context value does not block the role

- **WHEN** a canonical role property or metadata-only attribute supplies an empty string for a context key
- **THEN** the classifier omits that key, retains an otherwise valid role, and records a metadata-extraction failure

#### Scenario: Assembly metadata supplies a missing type context key

- **WHEN** an assembly has a canonical metadata-only attribute for a key absent from a type's winning role evidence
- **THEN** the resolved role fact receives the assembly value as a default

#### Scenario: Type context overrides assembly default

- **WHEN** a type and its assembly declare different values for the same canonical metadata key
- **THEN** the type value wins without a conflict because type-scope metadata is more specific

#### Scenario: Same-scope context disagreement is visible

- **WHEN** multiple canonical metadata evidence sources at the same specificity provide different values for one key
- **THEN** the model reports a deterministic metadata conflict, omits that key, and retains an otherwise valid primary role

#### Scenario: Context metadata does not create another role

- **WHEN** a type carries canonical context metadata in addition to its role evidence
- **THEN** the role index still exposes at most one primary role and exposes the context values only as metadata
