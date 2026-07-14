# policy-import-composition Specification

## Purpose
Defines deterministic, portable composition of one explicitly selected
architecture policy root with local YAML fragments while preserving a complete,
schema-valid effective policy and source provenance.
## Requirements
### Requirement: One explicit path determines the sole root policy
The system SHALL accept exactly one explicit policy path from each CLI, Testing adapter, or application-service run and SHALL treat that document as the sole root policy. The root document SHALL remain a normal executable policy and SHALL NOT derive its role from its filename or extension pattern.

#### Scenario: Arbitrary root filename
- **WHEN** a user selects `config/my-policy.yaml` as the policy path
- **THEN** the system treats that file as the root exactly as it would `architecture/arch.yml`

#### Scenario: Recommended root filename is conventional
- **WHEN** a user selects `architecture/arch.yml`
- **THEN** no behavior or schema rule is enabled solely because of that filename

### Requirement: Imports use an explicit ordered field
The root and fragments SHALL support a top-level `imports` sequence containing
non-empty explicit portable relative file paths. Each path SHALL resolve
relative to the document that declares it. An import string SHALL be Unicode
NFC and contain one or more non-empty `/`-separated segments. A segment SHALL
be `.` or `..`, or a portable file-name segment that contains no control
character or `<`, `>`, `:`, `"`, `/`, `\\`, `|`, `?`, or `*`; does not end in dot
or space; and whose case-insensitive basename before its first `.` is not a
Windows reserved device name: `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`,
`LPT1`–`LPT9`, `COM¹`–`COM³`, or `LPT¹`–`LPT³`. Backslashes, leading slashes,
empty segments, drive/URI colons, non-NFC strings, and interpolation tokens
(`${`, `$(`, `%...%`, or a leading `~`) SHALL be rejected. The grammar SHALL be
validated before any host filesystem resolution. Absolute paths, UNC/device
paths, URI-like values, globs, environment interpolation, and non-scalar
entries SHALL be rejected.

#### Scenario: Root combines inline content with imports
- **WHEN** a root defines ordinary `layers`, `analysis`, or `contracts` content
  and an ordered `imports` sequence
- **THEN** the root inline content and imported fragment content participate in
  one composed policy

#### Scenario: Portable slash-separated path
- **WHEN** an import entry is `policy/contracts/domain.yml`
- **THEN** every supported host treats it as the same sequence of relative
  segments before canonical filesystem resolution

#### Scenario: Platform-native or interpolation form
- **WHEN** an import uses `\\`, `/etc/policy.yml`, `C:\\policy.yml`,
  `\\\\server\\share\\policy.yml`, `\\\\?\\C:\\policy.yml`, a URI, a glob, or
  an interpolation token
- **THEN** policy loading fails before the target is read with a portable-path
  grammar diagnostic

#### Scenario: Reserved basename has an extension
- **WHEN** an import segment is `NUL.yml`, `COM1.arch.yml`, `LPT¹.yaml`, or
  `NUL.tar.gz`
- **THEN** policy loading rejects the segment before filesystem resolution on
  every supported host

### Requirement: Fragment role and shape come from the import graph
Every document reached through `imports` SHALL be validated as a fragment
regardless of filename. A fragment MAY contain `imports`, `layers`,
`external_dependencies`, `packages`, `legacy_runtime_layers`, `analysis`,
`contracts`, and `classification`; it SHALL contain at least one mergeable
section or a non-empty `imports` sequence. A fragment SHALL NOT contain
`version`, `name`, baseline content, or unknown top-level fields. The explicitly
selected root SHALL be validated as a root source document: it SHALL declare
`version` and `name`, and it MAY declare `imports` and every existing root
policy section. Root-source validation SHALL not require sections that can be
contributed by fragments.

#### Scenario: Arbitrary fragment filename
- **WHEN** `architecture/policy.yml` imports `parts/domain.yaml`
- **THEN** `parts/domain.yaml` is treated as a fragment even though it does not
  match `*.arch.yml`

#### Scenario: Root-only field in fragment
- **WHEN** an imported document declares `version` or `name`
- **THEN** loading fails with a fragment-shape diagnostic naming that field and
  source

#### Scenario: Root defers a required effective section
- **WHEN** a root declares `version`, `name`, and `imports`, while fragments
  provide `layers`, `analysis`, and `contracts`
- **THEN** root-source validation succeeds and the complete graph proceeds to
  effective-policy validation

### Requirement: Nested import expansion is deterministic and bounded
The system SHALL compose root inline content first, then expand imports using depth-first pre-order in each document's declared import order. A fragment's inline content SHALL precede the recursively expanded content of its imports. The root SHALL be depth 0; import depth SHALL NOT exceed 16 edges; and the graph SHALL NOT exceed 256 files including the root.

#### Scenario: Stable nested order
- **WHEN** the root imports A then B and A imports C then D
- **THEN** ordered content is composed as root, A, C, D, B

#### Scenario: Import graph exceeds a limit
- **WHEN** resolving the next import would exceed depth 16 or 256 total files
- **THEN** loading fails before reading the over-limit file with the limit and import chain in the diagnostic

### Requirement: Imports remain within one resolved root boundary
The system SHALL resolve the allowed import boundary once from the explicit
root policy. If the selected root lives directly under a directory named
`architecture`, the boundary SHALL be that directory's parent. Otherwise, the
boundary SHALL be the selected root's own directory. Each import SHALL be made
absolute, physically canonicalized, and verified to remain within that
boundary before being read. Authored path casing SHALL exactly match the
filesystem entry, and canonical boundary-relative portability identities SHALL
be compared case-insensitively.

#### Scenario: Relative in-bound import for an architecture root
- **WHEN** the selected root is `architecture/arch.yml`
- **AND WHEN** a fragment imports `../shared/layers.yml`
- **AND WHEN** its physically canonical target remains inside the parent of `architecture/`
- **THEN** the system reads that target using its canonical boundary-relative identity

#### Scenario: Relative import escapes a non-architecture root directory
- **WHEN** the selected root is `config/company-policy.yaml`
- **AND WHEN** it imports `../shared/layers.yml`
- **THEN** loading fails because the import resolves outside that root directory's boundary

#### Scenario: Link escapes repository
- **WHEN** an apparently in-bound import traverses a symbolic link or junction to a file outside the repository boundary
- **THEN** loading fails with an out-of-bound import diagnostic

#### Scenario: Path casing differs
- **WHEN** authored path casing differs from the on-disk path casing
- **THEN** loading fails consistently, including on a case-insensitive filesystem

### Requirement: Duplicate paths and cycles fail deterministically
The resolver SHALL maintain an active import stack and a completed canonical-path set. Reaching a path already on the active stack SHALL be reported as a cycle; reaching a previously completed physical file or case-insensitive portability identity SHALL be reported as a duplicate import. The resolver SHALL NOT silently deduplicate either case.

#### Scenario: Import cycle
- **WHEN** root imports A, A imports B, and B imports A
- **THEN** loading fails with the ordered cycle chain

#### Scenario: Same file through aliases
- **WHEN** two import entries resolve physically to the same file
- **THEN** loading fails with both authored paths and the canonical path

### Requirement: Keyed definitions merge without precedence
The system SHALL union `layers`, `external_dependencies`, `packages`, and `analysis.condition_sets` by ordinal case-sensitive key across the composed source order. A key declared more than once SHALL be a conflict even when both values are structurally identical. Root-inline, parent-fragment, and nested-fragment declarations SHALL follow the same rule.

#### Scenario: Distinct layer keys compose
- **WHEN** root declares layer `application` and an imported fragment declares layer `domain`
- **THEN** both layers exist in the composed policy

#### Scenario: Root and fragment repeat a key
- **WHEN** root and a fragment both declare layer `domain`
- **THEN** loading fails and identifies both declarations without applying root or imported precedence

### Requirement: Ordered collections preserve composed source order
The system SHALL concatenate `legacy_runtime_layers`, analysis assembly/path/project lists, classification mapping lists, and each registered strict/audit contract list in composed source order. The composer SHALL NOT sort, deduplicate, or merge entries within these lists. Each contract entry, including its `ignored_violations`, SHALL remain an atomic node.

#### Scenario: Contracts retain root and import order
- **WHEN** root declares contract R and its imports contribute contracts A, C, D, and B in expansion order
- **THEN** the resulting family list order is R, A, C, D, B

#### Scenario: Ignored violations stay with their contract
- **WHEN** an imported contract contains `ignored_violations`
- **THEN** those entries retain the contract's source and are not combined with another contract

### Requirement: Singleton settings reject multiple declarations
Each scalar or singleton analysis setting and `classification.precedence` SHALL have at most one explicit declaration across the graph. Default values SHALL be applied only after composition. Multiple declarations SHALL fail even when values are equal.

#### Scenario: Fragment owns one analysis singleton
- **WHEN** exactly one fragment declares `analysis.configuration` and no other source declares it
- **THEN** the composed policy uses that explicit value

#### Scenario: Equal singleton repeated
- **WHEN** root and a fragment both declare the same `analysis.configuration` value
- **THEN** loading fails with both source locations rather than silently accepting one

### Requirement: Contract ID compatibility extends across fragments
Fallback contract IDs SHALL be assigned after composition. IDs SHALL be compared case-insensitively and SHALL remain unique within the same registered contract family and mode across every source file. The same ID SHALL remain allowed in different families or in strict versus audit groups, preserving current monolithic-policy behavior.

#### Scenario: Duplicate ID in one family and mode across fragments
- **WHEN** two fragments contribute strict dependency contracts whose IDs differ only by case
- **THEN** loading fails and identifies both contract sources

#### Scenario: Same ID in compatible groups
- **WHEN** one source uses ID `boundary` in strict dependency and another uses it in audit dependency or strict layer
- **THEN** loading succeeds with respect to duplicate-ID validation

### Requirement: Composed nodes retain source provenance
Every root-inline and imported composed node SHALL retain typed provenance containing
the explicit root identity, canonical repository-relative source path using `/`,
graph-derived document role, YAML/logical property path, and composed source ordinal.
Imported nodes SHALL additionally retain their authored import edge and concise import
chain from the explicit root. Contract nodes and nested ignored violations SHALL retain
their contract family and effective contract ID where applicable. Provenance SHALL
survive composition, deserialization, fallback ID assignment, semantic validation,
configuration checking, policy-consistency checking, graph/explain setup, and Testing
adapter loading without storing machine-specific absolute paths in public output.

Conflict diagnostics SHALL carry both the original and conflicting typed locations.
Shape, effective-schema, semantic, missing-reference, consistency, and contract-family
diagnostics SHALL carry the typed location of the node that introduced the invalid
value. Human diagnostics SHALL retain root-policy context while identifying fragment
locations, and machine-readable ordering SHALL follow composed source order.

#### Scenario: Conflicting definitions name both sources
- **WHEN** two files declare the same keyed definition, singleton setting, or contract ID
- **THEN** the diagnostic carries both canonical source paths and YAML paths in original-then-conflicting order

#### Scenario: Invalid imported contract retains origin
- **WHEN** an imported contract fails an existing family validator after composition
- **THEN** the diagnostic carries the fragment role, portable fragment path, contract YAML path, family, and effective ID rather than only the root path

#### Scenario: Invalid inline root value retains arbitrary root identity
- **WHEN** the explicitly selected root has an arbitrary filename and an inline value fails validation
- **THEN** the diagnostic identifies that path as a root-role location without consulting its filename pattern

#### Scenario: Nested resolution failure retains import chain
- **WHEN** a nested import is missing, outside the boundary, cyclic, duplicated, or over a graph limit
- **THEN** the typed diagnostic carries a concise ordered import chain beginning with the explicit root identity

#### Scenario: Monolithic policy gains compatible provenance
- **WHEN** an existing policy contains no imports
- **THEN** its validation behavior and existing diagnostic fields remain compatible and its resolved nodes expose equivalent root-role typed provenance

#### Scenario: Renaming sources changes only displayed paths
- **WHEN** root or fragment files are renamed without changing content or graph relationships
- **THEN** diagnostic categories, contract behavior, YAML paths, and ordering are unchanged while portable source paths reflect the new names

#### Scenario: Every entry point consumes one provenance model
- **WHEN** the same imported policy is loaded through CLI validation, the Testing adapter, graph, or explain
- **THEN** each flow consumes the same graph-derived resolved provenance without reclassifying documents from filenames

### Requirement: Root and fragment schemas do not depend on filenames
The system SHALL publish a root-policy schema and a fragment schema that share policy definitions. Runtime schema selection SHALL use graph role. Editor support SHALL allow explicit schema selection, including an inline YAML language-server schema directive, without requiring root or fragment filename patterns.

#### Scenario: Editor validates arbitrary fragment name
- **WHEN** an arbitrary YAML file selects the fragment schema explicitly
- **THEN** editor validation accepts fragment sections and rejects root-only fields without filename association

#### Scenario: Runtime ignores naming convention
- **WHEN** recommended and alternative filenames contain equivalent root and fragment documents
- **THEN** runtime validation and composition results are identical

### Requirement: Monolithic policies and semantic values remain compatible
An existing policy without `imports` SHALL load and execute through the same document validation and family-binding behavior regardless of filename. CEL expressions and other future semantic scalar fields SHALL compose only as values within their owning nodes and SHALL NOT gain filesystem, import-graph, templating, or cross-file anchor access. Baseline files SHALL remain separate from policy import composition.

#### Scenario: Existing single-file policy
- **WHEN** a valid existing monolithic policy contains no `imports`
- **THEN** it loads without migration and produces the same effective document and behavior

#### Scenario: CEL-bearing imported node
- **WHEN** a future fragment contributes a schema-approved CEL expression field
- **THEN** the expression is validated after composition with its source provenance and cannot inspect or alter import resolution

### Requirement: Composed effective policy is validated before semantic loading
After all sources pass role/shape validation and are composed, the system SHALL
validate the composed document against the full effective-policy schema before
fallback contract ID assignment and before the ordered
`IArchitecturePolicyDocumentValidator` pipeline. The full schema SHALL require
`version`, `name`, `layers`, `analysis`, and `contracts`, matching the current
production policy schema. The effective-policy schema SHALL validate the
composed document rather than any individual source.

#### Scenario: Fragments complete the root
- **WHEN** a root source omits `layers`, `analysis`, and `contracts` but its
  fragments compose all three sections
- **THEN** the full effective-policy schema accepts the composed document before
  fallback IDs and semantic validators run

#### Scenario: Required effective section absent from graph
- **WHEN** no source contributes one of `layers`, `analysis`, or `contracts`
- **THEN** loading fails against the effective-policy schema before fallback ID
  assignment and family-specific semantic validation

### Requirement: Raw classification sections compose without lost behavior
The composer SHALL preserve raw `classification.path`,
`classification.overrides`, and `classification.exclusions` nodes from every
role-valid source with their source descriptors and composed order.
`classification.path` entry counts SHALL be aggregated across the complete
graph; a non-zero aggregate SHALL set `ClassificationPathDeferred` to that
count, preserving the existing deferred-support diagnostic. `overrides` and
`exclusions` SHALL remain schema-valid, provenance-preserving deferred no-ops
until their separately specified runtime behavior exists. Model-bound
classification lists (`attributes`, `assembly_attributes`, `inheritance`, and
`namespace`) SHALL append in composed order, while `precedence` remains a
singleton conflict-checked field.

#### Scenario: Imported path entries remain visible
- **WHEN** root and fragments contribute two and three valid
  `classification.path` entries respectively
- **THEN** the composed document exposes `ClassificationPathDeferred` with a
  declared entry count of five and the diagnostics retain the contributing
  sources

#### Scenario: Deferred raw entries do not gain silent semantics
- **WHEN** a fragment declares schema-valid `classification.overrides` or
  `classification.exclusions`
- **THEN** the nodes are retained with provenance and composed order but do not
  alter classification behavior before their separate feature is implemented

### Requirement: Import loading failures expose stable categories
Policy loading SHALL expose a stable programmatic category for failures caused by portable-path validation, missing import targets, repository-boundary violations, authored path-case mismatches, import cycles, duplicate canonical imports, graph limits, source-role shape violations, and composition conflicts. Diagnostic text SHALL identify the relevant source or import chain without making callers parse that text to determine the category.

#### Scenario: Caller distinguishes a cycle from a duplicate import
- **WHEN** one policy load reaches an active source again and another reaches an already completed source
- **THEN** the two failures expose distinct cycle and duplicate-import categories

#### Scenario: Composition conflict is categorized
- **WHEN** two composed sources declare the same keyed definition or singleton setting
- **THEN** policy loading fails with the composition-conflict category and identifies both declarations

### Requirement: Typed policy diagnostics reach every reporting boundary
Import and policy-validation exceptions carrying typed diagnostics SHALL retain
their policy location, related locations, and import chain through the CLI
boundary. Human output SHALL identify the policy source and explicit root;
JSON and SARIF output SHALL expose machine-readable location data rather than
only an exception string.

#### Scenario: Imported effective-schema value is invalid
- **WHEN** an imported fragment with an arbitrary filename contributes an
  effective-policy value with an invalid type or shape
- **THEN** the CLI human output identifies the fragment and root, and JSON and
  SARIF output contain the typed fragment policy location and import chain

### Requirement: Expanded layer templates retain source-template provenance
The system SHALL retain source-template typed provenance for every strict or
audit layer template expanded for execution or policy-consistency checking.
The retained provenance SHALL include source path, YAML path, family, effective
ID, and root context.

#### Scenario: Imported strict template produces a violation
- **WHEN** an imported strict layer template expands into a contract that
  produces a runtime violation
- **THEN** human, JSON, SARIF, and Testing adapter outputs identify the source
  template location from the fragment

#### Scenario: Imported audit template participates in consistency checking
- **WHEN** an imported audit layer template expands into a policy-consistency
  finding
- **THEN** the finding identifies the source template location from the
  fragment

### Requirement: Provenance location order reflects composition encounter order
Primary and related policy locations SHALL be ordered by their composed
encounter ordinal, not lexicographic YAML path. For composition conflicts, the
original declaration SHALL be primary and the conflicting declaration SHALL be
related.

#### Scenario: Double-digit contract index participates in a conflict
- **WHEN** related locations include composed nodes whose display YAML paths
  contain indices 2 and 10
- **THEN** their order follows composed encounter order, and the original
  declaration remains the primary location

### Requirement: Catalog-expanded templates retain their source owner
The system SHALL bind provenance to the exact layer-template contract instances
materialized by the contract catalog. Runtime and consistency diagnostics for
imported strict and audit templates SHALL identify their source template.

#### Scenario: Catalog re-expands an imported template
- **WHEN** catalog construction expands an imported layer template after policy
  provenance binding
- **THEN** a violation from the catalog instance retains the fragment template
  location

### Requirement: Provenance order follows effective encounter order
The system SHALL order primary and related policy locations by effective-node encounter order, including nodes from the same source document.

#### Scenario: Double-digit sequence indices
- **WHEN** locations from one source include sequence indices 2 and 10
- **THEN** the output preserves their composed order rather than lexical path order

### Requirement: Template expansion failures are typed policy diagnostics
The system SHALL enrich invalid imported exhaustive layer-template expansion failures with the source template provenance before CLI reporting.

#### Scenario: Dotted exhaustive template layer
- **WHEN** an imported exhaustive template declares a dotted layer name
- **THEN** JSON and SARIF report the typed fragment template location

### Requirement: Specialized provenance diagnostics preserve encounter order
The system SHALL order classification path and every other specialized provenance location by source ordinal and encounter ordinal.

#### Scenario: Classification path has double-digit indices
- **WHEN** one source has entries at indices 2 and 10
- **THEN** human and JSON diagnostics retain authored order

### Requirement: Expanded template provenance uses exact source identity
The system SHALL bind each generated layer-template contract to the authored template identified by its stable owner identity.

#### Scenario: Same-name templates have distinct IDs
- **WHEN** root and fragment templates share a name but have distinct explicit IDs
- **THEN** each generated contract reports its own authored source

### Requirement: Root parsing failures retain typed source provenance
The selected root SHALL receive a root source descriptor before YAML parsing
decides whether it contains imports. A malformed, multi-document, or non-mapping
root SHALL fail with a typed source-shape diagnostic whose location identifies
that root path and root role.

#### Scenario: Malformed selected root
- **WHEN** the explicitly selected root YAML is syntactically malformed
- **THEN** loading fails with a typed source-shape diagnostic for the root

#### Scenario: Root is not one mapping document
- **WHEN** the explicitly selected root contains multiple documents or a
  non-mapping document
- **THEN** loading fails with a typed source-shape diagnostic for the root

### Requirement: Raw composed YAML validation retains source provenance
Raw YAML validation that must run before DTO deserialization SHALL execute with
the composed node's provenance as its active validation subject. An imported
raw layer, contextual-contract, port-boundary, or semantic-coverage validation
failure SHALL produce a typed semantic-validation diagnostic for the fragment
node that introduced it.

#### Scenario: Imported blank layer namespace
- **WHEN** an imported fragment declares a layer namespace containing only
  whitespace
- **THEN** loading fails with a typed semantic-validation diagnostic identifying
  that fragment layer

#### Scenario: Imported raw selector field is invalid
- **WHEN** an imported contextual or port-boundary contract contains a raw
  selector field rejected before DTO deserialization
- **THEN** loading fails with a typed semantic-validation diagnostic identifying
  that fragment contract

### Requirement: Internal provenance identity is collision-safe
The provenance index SHALL use escaped JSON Pointer identity for effective
nodes. Display-oriented dot/index YAML paths SHALL be retained only in
`ArchitecturePolicySourceLocation.YamlPath` and SHALL NOT be used for lookup or
ownership binding. Legal mapping keys containing dots, brackets, numeric text,
`~`, or `/` SHALL not overwrite another node's provenance.

#### Scenario: Dot-containing layer key does not collide with nested path
- **WHEN** a root contributes layer key `a.namespace` and a fragment contributes
  layer `a` with a `namespace` property
- **THEN** an effective-schema error for `a.namespace` identifies the root
  declaration rather than the fragment property

#### Scenario: Escaped mapping key retains its source
- **WHEN** a legal composed mapping key contains `~` or `/`
- **THEN** its provenance lookup resolves to the source that declared that key

### Requirement: Consistency provenance selects identified contracts

The policy provenance index SHALL select only contract declarations whose IDs
participate in a policy-consistency diagnostic when that diagnostic supplies a
primary contract ID or one or more conflicting contract IDs. It SHALL NOT
select a contract solely because its display name matches. Name-based selection
MAY be used only when the diagnostic supplies no participating contract IDs.

#### Scenario: Same display name appears in a different contract family

- **WHEN** a consistency conflict identifies two dependency contracts by ID
- **AND WHEN** an unrelated contract in another family has the same display
  name as one of those contracts
- **THEN** the diagnostic's primary and related policy locations identify only
  the two participating dependency contracts

### Requirement: Public import authoring guidance is complete and filename-neutral
ArchLinterNet SHALL publish public guidance that explains one explicitly selected root, ordered local `imports`, graph-derived fragment roles, allowed fragment sections, root-inline composition, deterministic nested order, merge and conflict rules, repository path boundaries, graph limits, diagnostics, unsupported behavior, and the distinction between naming conventions and runtime requirements. The guidance SHALL recommend `architecture/arch.yml` for a concise stable root and concern-specific `*.arch.yml` fragment names without describing those names or `architecture/dependencies.arch.yml` as mandatory.

#### Scenario: Author chooses recommended names
- **WHEN** an author follows the recommended root and fragment naming conventions
- **THEN** the documentation explains that behavior comes from the selected root path and import edges rather than those names

#### Scenario: Author chooses arbitrary names
- **WHEN** an author selects `config/company-policy.yaml` and imports `pieces/domain.data`
- **THEN** the documentation and examples describe behavior equivalent to a recommended-name graph

#### Scenario: Author checks unsupported behavior
- **WHEN** an author considers multiple roots, remote imports, globs, silent overrides, environment interpolation, arbitrary YAML tags, or cross-file anchors
- **THEN** the public guidance identifies each behavior as unsupported

### Requirement: Public schema, migration, and troubleshooting guidance covers both roles
ArchLinterNet SHALL document explicit root and fragment schema selection for common schema-aware editors without requiring filename associations. It SHALL provide a behavior-preserving migration from a monolithic policy to one root plus focused fragments and troubleshooting for missing imports, cycles, duplicate paths or IDs, composition conflicts, path-boundary violations, invalid fragment shapes, and editor schema association.

#### Scenario: Editor validates an arbitrary fragment filename
- **WHEN** an author assigns `schema/dependencies.arch.fragment.schema.json` explicitly to an arbitrary imported file
- **THEN** editor validation uses fragment shape without relying on a filename pattern

#### Scenario: Monolithic policy is migrated incrementally
- **WHEN** an author moves one concern at a time from a valid monolithic policy into imported fragments
- **THEN** the guide preserves one root, global contract identity, composition order, and equivalent validation behavior at each checked step

### Requirement: Committed acceptance fixtures prove public import behavior
The repository SHALL contain executable NUnit-backed fixtures that prove equivalent monolithic and imported policies produce equivalent validation outcomes, recommended and arbitrary filenames produce equivalent outcomes, and root-versus-fragment plus fragment-versus-fragment conflicts fail without silent precedence.

#### Scenario: Equivalent public fixtures load
- **WHEN** the acceptance suite loads the monolithic, recommended-name imported, and arbitrary-name imported fixtures
- **THEN** their behaviorally relevant resolved models and validation outcomes are equivalent

#### Scenario: Conflicting public fixtures load
- **WHEN** the acceptance suite loads root-versus-fragment or fragment-versus-fragment duplicate definitions
- **THEN** loading fails with a composition-conflict category identifying both participating sources
