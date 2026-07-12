## MODIFIED Requirements

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

#### Scenario: Two inheritance mapping entries matching one type via different base types resolve by declaration order
- **WHEN** two `classification.inheritance` entries both match one type (e.g. one matching a base class, another matching an interface the type also implements) and assign different roles
- **THEN** the model assigns the first-declared entry's role and records the discarded entry's role as a `conflict` fact

### Requirement: Runtime behavior is introduced only for implemented classification sources and layer selectors
The runtime SHALL execute `classification.attributes`, `classification.assembly_attributes`, `classification.inheritance`, `classification.namespace`, and `layers.<name>.selector` matching/binding according to their implemented capabilities. `classification.path`, `classification.overrides`, and `classification.exclusions` remain schema-valid reserved constructs until their own execution capabilities land; `classification.path` additionally produces a deterministic deferred-support diagnostic when declared (see the diagnostic scenario below), distinguishing it from the fully silent `overrides`/`exclusions` reserved sections.

#### Scenario: Declaring reserved unimplemented classification constructs does not throw
- **WHEN** a policy declares `classification.overrides`, `classification.exclusions`, or `classification.path` before their implementation lands
- **THEN** policy loading and validation SHALL proceed without exception and those reserved constructs SHALL produce no role assignment yet

#### Scenario: Declaring classification.attributes or classification.assembly_attributes now produces role or metadata assignments
- **WHEN** a policy declares `classification.attributes` or `classification.assembly_attributes` entries matching attributes present in scanned code
- **THEN** the extraction engine assigns role or metadata per the `attribute-role-extraction` capability, rather than treating the declaration as an inert no-op

#### Scenario: Declaring classification.inheritance now produces role or metadata assignments
- **WHEN** a policy declares a `classification.inheritance` entry whose `base_type` names a type that a scanned type derives from or implements, transitively
- **THEN** the extraction engine assigns that entry's declared `role` (and any successfully extracted `metadata`) to the matching type, with `Evidence` set to the matched `base_type` full name

#### Scenario: Declaring classification.namespace now produces role or metadata assignments
- **WHEN** a policy declares a `classification.namespace` entry whose `namespace`/`namespace_suffix` matches a scanned type's namespace, using the same glob semantics already accepted by `layers.<name>.namespace`
- **THEN** the extraction engine assigns that entry's declared `role` (and any successfully extracted `metadata`) to the matching type, with `Evidence` set to the matched namespace pattern

#### Scenario: Inheritance metadata extraction is restricted to literal and const forms
- **WHEN** a `classification.inheritance` or `classification.namespace` entry declares a `metadata.<key>` value
- **THEN** the value is interpreted only as a literal YAML scalar or a `const:<Full.Type.NAME>` reference — `constructor[<index>]` and `property:<Name>` forms are not valid for these sources, since neither has an attribute instance to extract from

#### Scenario: Unresolved inheritance base_type produces no match and no diagnostic
- **WHEN** a `classification.inheritance` entry's `base_type` names no type in the scanned type universe
- **THEN** that entry matches no type for the run, consistent with how an unresolved `const:` reference is silently omitted rather than diagnosed

#### Scenario: Declaring classification.path produces a deferred-support diagnostic
- **WHEN** a policy declares a non-empty `classification.path` section
- **THEN** policy loading SHALL proceed without exception, no role assignment SHALL result from `path` entries, and the model SHALL surface a non-blocking, informational diagnostic explaining that path-convention classification is not yet implemented pending source/declared-type fact discovery, visible even when the scanned type universe is empty

### Requirement: Existing policies remain unaffected
A policy with no `classification` section and no `layers.<name>.selector` field SHALL behave identically to its behavior before the classification model existed. A policy declaring only `classification.attributes`/`classification.assembly_attributes` (no `inheritance`/`namespace`/`path`) SHALL behave identically to its behavior before `inheritance`/`namespace` execution landed.

#### Scenario: Policy without classification is unaffected
- **WHEN** a policy declares no `classification` section and no layer uses `selector`
- **THEN** no classification-related schema field constrains that policy beyond what already applied before this change

#### Scenario: Policy using only attribute-based sources is unaffected by inheritance/namespace execution
- **WHEN** a policy declares `classification.attributes`/`classification.assembly_attributes` but no `classification.inheritance`/`classification.namespace` entries
- **THEN** role/metadata assignment for that policy is identical to its behavior before `classification.inheritance`/`classification.namespace` execution landed
