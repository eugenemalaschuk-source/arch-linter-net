## ADDED Requirements

### Requirement: Native topology is an opt-in, schema-backed policy model
The effective policy document SHALL support an optional native `topology`
section. A present topology SHALL declare a mode of `partial` or `exhaustive`,
one explicit observed `subject_kind` of `type`, `namespace`, `project`, or
`assembly`, and one non-empty bounded subject scope. Policies that omit the
section SHALL retain their existing loading and governance behavior.

#### Scenario: Existing policy remains compatible
- **WHEN** a valid pre-topology policy omits `topology`
- **THEN** it loads and evaluates with the same behavior it had before native topology support

#### Scenario: Exhaustive topology names its universe
- **WHEN** a policy declares an exhaustive topology
- **THEN** it declares one subject kind and a non-empty bounded scope rather than relying on evaluator defaults

### Requirement: Nodes and mappings have stable, closed selector semantics
Each topology node SHALL have a unique stable ID and one or more mapping
selectors. A topology scope, node mapping, or out-of-scope declaration SHALL
use exactly one supported primary selector: a declared layer, an existing
namespace pattern, a project identity, an assembly identity, or an existing
semantic-context selector. Namespace suffix and semantic-context metadata/CEL
semantics SHALL reuse their existing policy contracts; topology SHALL NOT add a
second unrestricted query language.

#### Scenario: Semantic role maps a type topology node
- **WHEN** a type topology node maps with a semantic-context selector naming a role and metadata
- **THEN** the declaration reuses the existing context selector shape and has a deterministic node identity

#### Scenario: Invalid selector shape fails policy load
- **WHEN** a topology selector has no primary selector, has multiple primary selectors, or references an undeclared layer
- **THEN** policy loading fails with an actionable provenance-aware diagnostic

### Requirement: Topology mappings classify subjects deterministically
The native model SHALL classify each canonical observed subject fact from the topology evaluator independently of YAML order. A subject first
matches the declared universe, then is reviewed out of scope when it matches a
named out-of-scope declaration; otherwise it is mapped when exactly one node
matches, unmapped when no node matches, and ambiguous when multiple nodes
match. Canonical subject identity SHALL preserve the declared kind together
with the supplied project, assembly, and subject identities so same-named
subjects are not conflated.

#### Scenario: Mapping order does not alter classification
- **WHEN** equivalent node and mapping declarations are reordered in YAML
- **THEN** each observed subject receives the same mapped, unmapped, ambiguous, or reviewed-out-of-scope classification

#### Scenario: Explicit reviewed out-of-scope subject is not an unmapped subject
- **WHEN** an in-scope observed subject matches a named, reasoned reviewed out-of-scope declaration
- **THEN** it is classified as reviewed out of scope and is not counted as an unmapped node mapping

### Requirement: Exhaustive completeness and declaration drift are explicit
In `partial` mode, undeclared observed subjects SHALL not implicitly be a
completeness failure. In `exhaustive` mode, every in-scope observed subject
SHALL be mapped exactly once or explicitly reviewed out of scope. The scope
shall expose an explicit `allow_empty` setting; an exhaustive topology with no
observed subjects SHALL be insufficient required evidence unless that setting
permits the empty case. `stale_declarations` SHALL explicitly control whether
unmatched nodes and unused directional edges produce stale-declaration
evidence, distinct from unmapped observed subjects.

#### Scenario: Partial topology leaves an undeclared subject partial
- **WHEN** a partial topology has an in-scope observed subject matching no node
- **THEN** the model does not assert an exhaustive completeness failure solely from that absence

#### Scenario: Exhaustive topology cannot discard an observed subject
- **WHEN** an exhaustive topology has an in-scope observed subject that matches no node and no reviewed out-of-scope declaration
- **THEN** the model requires unmapped-subject applicability evidence rather than a clean topology result

#### Scenario: Empty required universe composes with fail-closed applicability
- **WHEN** an exhaustive topology observes no subjects and `allow_empty` is false
- **THEN** the model requires the existing unexpected-empty applicability semantics rather than clean zero findings

### Requirement: Topology declarations are internally consistent and directional
Allowed topology edges SHALL be directional unique pairs of declared node IDs.
Node IDs, edge pairs, out-of-scope IDs, and exact duplicate mapping declarations
SHALL be validated deterministically with actionable diagnostics. Exact
duplicate mappings across distinct nodes SHALL be rejected as an invalid
ambiguous declaration; overlap that depends on observed facts SHALL remain
explicit ambiguous-subject evidence for the evaluator.

#### Scenario: Duplicate directional edge fails policy load
- **WHEN** the same source and target node pair is declared more than once
- **THEN** policy loading fails instead of relying on enumeration order

#### Scenario: Stale node stays distinct from a new unmapped subject
- **WHEN** stale declarations are enabled and a declared node has no current mapping while another observed subject maps to no node
- **THEN** the model requires separate stale-declaration and unmapped-subject evidence
