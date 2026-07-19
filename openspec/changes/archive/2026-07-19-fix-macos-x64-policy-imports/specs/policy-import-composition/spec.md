## MODIFIED Requirements

### Requirement: Imports remain within one resolved root boundary
The system SHALL resolve the allowed import boundary once from the explicit
root policy. If the selected root lives directly under a directory named
`architecture`, the boundary SHALL be that directory's parent. Otherwise, the
boundary SHALL be the selected root's own directory. Each import SHALL be made
absolute, physically canonicalized, and verified to remain within that
boundary before being read. Authored path casing SHALL exactly match the
filesystem entry, and canonical boundary-relative portability identities SHALL
be compared case-insensitively. On macOS, source identity SHALL be derived
through managed canonical filesystem operations without a Darwin
processor-specific unmanaged metadata layout.

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

#### Scenario: Intel macOS loads a valid imported policy
- **WHEN** a macOS x86_64 host loads a valid root policy that imports one regular fragment
- **THEN** loading succeeds without relying on a Darwin unmanaged metadata layout

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
value. A failure resolving the explicitly selected policy SHALL identify that source
as the root policy. An imported-source failure SHALL identify its declaring fragment
and import edge with an ordered root-based import chain. Human diagnostics SHALL retain
root-policy context while identifying fragment locations, and machine-readable ordering
SHALL follow composed source order.

#### Scenario: Conflicting definitions name both sources
- **WHEN** two files declare the same keyed definition, singleton setting, or contract ID
- **THEN** the diagnostic carries both canonical source paths and YAML paths in original-then-conflicting order

#### Scenario: Invalid imported contract retains origin
- **WHEN** an imported contract fails an existing family validator after composition
- **THEN** the diagnostic carries the fragment role, portable fragment path, contract YAML path, family, and effective ID rather than only the root path

#### Scenario: Invalid inline root value retains arbitrary root identity
- **WHEN** the explicitly selected root has an arbitrary filename and an inline value fails validation
- **THEN** the diagnostic identifies that path as a root-role location without consulting its filename pattern

#### Scenario: Selected root cannot be resolved
- **WHEN** the user-selected root policy is missing, unreadable, non-regular, outside its boundary, or mismatched in case
- **THEN** the typed diagnostic identifies the selected source as a root policy rather than an import and has no declaring-fragment import edge

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
