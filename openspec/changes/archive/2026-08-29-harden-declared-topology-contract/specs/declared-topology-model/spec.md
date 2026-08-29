## MODIFIED Requirements

### Requirement: Nodes and mappings have stable, closed selector semantics
Each topology node SHALL have a unique stable ID and one or more mapping
selectors. A topology scope, node mapping, or out-of-scope declaration SHALL
use exactly one supported primary selector. The permitted selector kinds SHALL
be determined by the declared `subject_kind`: `type` permits layer, namespace,
project, assembly, and semantic-context selectors; `namespace` permits
namespace, project, and assembly selectors; `project` permits only project;
and `assembly` permits only assembly. For namespace subjects, project and
assembly selectors SHALL compare exact canonical owning project and assembly
identity on the namespace fact. Layer and semantic-context selectors SHALL be
rejected for non-type subjects rather than inferring an aggregate over types.
Namespace suffix and semantic-context metadata/CEL semantics SHALL reuse their
existing policy contracts; topology SHALL NOT add a second unrestricted query
language.

#### Scenario: Type topology reuses semantic role
- **WHEN** a type topology node maps with a semantic-context selector naming a role and metadata
- **THEN** the declaration reuses the existing context selector shape and has a deterministic node identity

#### Scenario: Namespace topology matches its canonical owner
- **WHEN** a namespace topology selector names a project or assembly
- **THEN** #509 can compare it as exact equality against that namespace fact's canonical owning project or assembly without inspecting contained types

#### Scenario: Aggregate selector is rejected
- **WHEN** a non-type topology uses a layer or semantic-context selector
- **THEN** policy loading fails with an actionable diagnostic naming the subject kind and selector kind

#### Scenario: Invalid selector shape fails policy load
- **WHEN** a topology selector has no primary selector, has multiple primary selectors, or references an undeclared layer
- **THEN** policy loading fails with an actionable provenance-aware diagnostic

### Requirement: Topology declarations are internally consistent and directional
Allowed topology edges SHALL be directional unique ordered pairs of declared
node IDs. Node IDs, edge pairs, out-of-scope IDs, and exact duplicate mapping
declarations SHALL be validated deterministically with actionable diagnostics.
Selector equality, ordering, and weakening comparison SHALL use structural
typed fields (including metadata key/value pairs) and SHALL NOT use
delimiter-composed string identities. Exact duplicate mappings across distinct
nodes SHALL be rejected as an invalid ambiguous declaration; overlap that
depends on observed facts SHALL remain explicit ambiguous-subject evidence for
the evaluator.

#### Scenario: Delimiter-bearing identities remain distinct
- **WHEN** two context selectors or two directional edge pairs differ structurally but their values contain comma, equals, semicolon, or arrow text
- **THEN** policy loading and deterministic ordering retain them as distinct declarations

#### Scenario: Duplicate directional edge fails policy load
- **WHEN** the same source and target node pair is declared more than once
- **THEN** policy loading fails instead of relying on enumeration order

#### Scenario: Stale node stays distinct from a new unmapped subject
- **WHEN** stale declarations are enabled and a declared node has no current mapping while another observed subject maps to no node
- **THEN** the model requires separate stale-declaration and unmapped-subject evidence
