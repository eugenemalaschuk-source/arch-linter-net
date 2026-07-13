# policy-import-composition Specification

## Purpose
TBD - created by archiving change design-single-root-policy-imports. Update Purpose after archive.
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
The root and fragments SHALL support a top-level `imports` sequence containing non-empty explicit relative file paths. Each path SHALL resolve relative to the document that declares it. Absolute paths, URI-like paths, globs, environment interpolation, and non-scalar entries SHALL be rejected.

#### Scenario: Root combines inline content with imports
- **WHEN** a root defines ordinary `layers`, `analysis`, or `contracts` content and an ordered `imports` sequence
- **THEN** the root inline content and imported fragment content participate in one composed policy

#### Scenario: Unsupported import expression
- **WHEN** an import entry is absolute, contains glob syntax, uses a URI, or requests environment interpolation
- **THEN** policy loading fails before the target is read

### Requirement: Fragment role and shape come from the import graph
Every document reached through `imports` SHALL be validated as a fragment regardless of filename. A fragment MAY contain `imports`, `layers`, `external_dependencies`, `packages`, `legacy_runtime_layers`, `analysis`, `contracts`, and `classification`; it SHALL contain at least one mergeable section or a non-empty `imports` sequence. A fragment SHALL NOT contain `version`, `name`, baseline content, or unknown top-level fields.

#### Scenario: Arbitrary fragment filename
- **WHEN** `architecture/policy.yml` imports `parts/domain.yaml`
- **THEN** `parts/domain.yaml` is treated as a fragment even though it does not match `*.arch.yml`

#### Scenario: Root-only field in fragment
- **WHEN** an imported document declares `version` or `name`
- **THEN** loading fails with a fragment-shape diagnostic naming that field and source

### Requirement: Nested import expansion is deterministic and bounded
The system SHALL compose root inline content first, then expand imports using depth-first pre-order in each document's declared import order. A fragment's inline content SHALL precede the recursively expanded content of its imports. The root SHALL be depth 0; import depth SHALL NOT exceed 16 edges; and the graph SHALL NOT exceed 256 files including the root.

#### Scenario: Stable nested order
- **WHEN** the root imports A then B and A imports C then D
- **THEN** ordered content is composed as root, A, C, D, B

#### Scenario: Import graph exceeds a limit
- **WHEN** resolving the next import would exceed depth 16 or 256 total files
- **THEN** loading fails before reading the over-limit file with the limit and import chain in the diagnostic

### Requirement: Imports remain within one repository boundary
The system SHALL resolve the allowed repository boundary once from the explicit root policy. Each import SHALL be made absolute, physically canonicalized, and verified to remain within that boundary before being read. Authored path casing SHALL exactly match the filesystem entry, and canonical repository-relative portability identities SHALL be compared case-insensitively.

#### Scenario: Relative in-bound import
- **WHEN** a fragment imports `../shared/layers.yml` and its physically canonical target remains inside the resolved repository root
- **THEN** the system reads that target using its canonical repository-relative identity

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
Every composed node SHALL retain the explicit root identity, its canonical repository-relative source path, and its YAML path. Conflict diagnostics SHALL identify both the original and conflicting declarations. Shape, semantic, and contract validation diagnostics SHALL identify the source and YAML path that introduced the invalid node. Diagnostic and machine-readable ordering SHALL follow composed order.

#### Scenario: Conflicting definitions name both sources
- **WHEN** two files declare the same keyed definition
- **THEN** the diagnostic reports both canonical source paths and YAML paths

#### Scenario: Invalid imported contract retains origin
- **WHEN** an imported contract fails existing family validation after composition
- **THEN** the diagnostic identifies the fragment and contract YAML path rather than only the root

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

