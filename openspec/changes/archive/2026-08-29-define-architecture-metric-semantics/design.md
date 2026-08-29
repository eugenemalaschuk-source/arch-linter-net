## Context

Issue #93 needs deterministic architecture facts before #517 can expose a
measure-first report and #518/#519 can introduce budgets. The repository
already has authorities for direct dependency relations, native declared
topology, project/assembly ownership, public API surface extraction, and
control-level applicability evidence. This issue defines how later work
combines those facts without implementing a competing graph, coverage model,
or output envelope.

## Goals / Non-Goals

**Goals:**

- Define a small closed metric catalog with an explicit subject, universe,
  identity, contributor set, and applicability condition for every metric.
- Make each value a cardinality of canonical architecture facts so repeated
  references, YAML order, and runtime enumeration cannot change it.
- Reuse #505's control-level applicability record and native evidence rather
  than treating zero as trustworthy by default.
- Make dependencies, component footprint, topology-slice type, and public
  contract-surface measurements explainable by deterministic contributors.

**Non-Goals:**

- Implement a policy schema, evaluator, report/CLI command, threshold,
  baseline, finding, output projection, or public API.
- Define a general code-quality score, cyclomatic-complexity metric, runtime
  measurement, test-coverage metric, formula language, or custom scripting.
- Recompute topology mapping, dependency graph facts, project discovery, API
  surface extraction, coverage, or effective-policy inventory.

## Decisions

### 1. Use a closed catalog of set-cardinality metrics

The initial catalog has six metric kinds: component outgoing dependency count,
component incoming dependency count, component external dependency-group count,
component project-or-assembly footprint count, topology-slice type count, and
public contract-surface size. A later policy schema may select only these named
kinds and their reviewed bounded subjects; it cannot accept formulas,
expressions, arbitrary aggregate callbacks, or user scripts.

Every metric value is `|S|`, where `S` is the relevant set of canonical
contributors. A contributor has an explicit stable identity and ordinal output
ordering. Repeated method-body references, repeated IL tokens, multiple paths,
and YAML ordering cannot add a second member to `S`.

Alternative considered: expose raw occurrence counts alongside a generic
aggregation expression. Rejected because those values reward implementation
detail rather than architecture structure and would create a second policy
language with non-deterministic explainability.

### 2. A logical component is a native topology node

Dependency and footprint metrics target one node from the effective native
`topology` declaration. Its canonical identity is the effective topology
provenance plus stable node ID; display text, YAML position, source-set
expansion, and a future metric control ID are not component identity. The
metric consumes the existing mapping of observed topology subjects to exactly
one node and derives direct relations by mapping both ends of the existing
direct dependency facts.

This keeps layers, namespaces, projects, and assemblies from becoming
interchangeable ad-hoc component keys. A future policy may expose only the
node-based target shape defined here; it does not infer a component by a broad
selector or a graph label.

Alternative considered: let every metric name any layer/namespace/assembly
string. Rejected because it would duplicate topology selectors and make
component identity and scope ambiguous.

### 3. Count direct, distinct component relations and external groups

Outgoing and incoming metrics use a directed set of mapped component pairs.
For an outgoing measurement of component `A`, contributors are distinct target
nodes `B` for direct observed relations `A -> B`; incoming reverses the pair.
Self-pairs (`A -> A`) are excluded. A cycle contributes only its real directed
edges: `A -> B` and `B -> A` each contribute once to their respective source
and target measurements, without transitive closure or a separate cycle bonus.

External-group metrics similarly count distinct declared external-group
identities with a direct source-component-to-group relation. They ignore
first-party component pairs and count no raw external reference occurrence.
An unmatched external reference is never collapsed into an invented
`other` group.

Alternative considered: count all type references emitted by reflection/IL.
Rejected because one logical dependency can be repeated many times and the
number changes with local implementation detail.

### 4. Footprint and topology-slice metrics use authoritative ownership and type facts

Component footprint uses the set of canonical owning project or resolved
assembly identities for all observed topology subjects mapped to the target
node. `project` and `assembly` are an explicit closed unit choice, not values
that can be mixed or summed. A type's identity is its existing canonical
first-party type fact (including its owning resolved assembly), not a simple
name. `topology-slice type count` is defined only for a `subject_kind: type`
topology and counts the distinct mapped type facts for one node.

Generated/test filtering and multi-targeting remain the responsibility of the
configured analysis snapshot and topology universe. Metrics add no implicit
filter. They use the snapshot's canonical project/assembly binding; an absent,
ambiguous, or not-deterministically-selected binding makes the applicable
measurement unassessable rather than unioning target frameworks or choosing an
arbitrary build result.

Alternative considered: scan project files or source files again for these
counts. Rejected because it would disagree with project discovery and the
analysis snapshot already trusted by architecture evaluation.

### 5. Public contract-surface size counts the selected observed export set

The metric targets one existing public API surface contract and counts the
distinct `(resolved assembly identity, normalized export signature)` pairs in
the contract's current selected observed export set. It reuses the same export
normalization, selector, assembly-resolution, and first-party dependency
integrity rules as public API surface evaluation. A future report can therefore
show the exact signature-pair contributors; a future budget sees intended
surface growth even when a policy's public-API contract is audit-mode.

The referenced public API contract remains the source of truth for governed
assemblies and selected-surface membership. Its required declaration/snapshot
inputs must be usable according to its existing contract semantics; a malformed
or unavailable source cannot be hidden by counting whatever reflection happens
to return.

Alternative considered: count source declarations or documentation pages.
Rejected because neither uses the reviewed public-API normalization nor
preserves exported-member semantics consistently across assemblies.

### 6. Assessability is a prerequisite to a trustworthy value

Each future metric control will produce the #505 applicability record with
native metric evidence. A measurement is evaluable only when its metric
definition, target subject/universe, required direct dependency or ownership
facts, and all contributor classifications needed by that metric are complete.
For component-relation counts, a direct relation that requires an unmapped or
ambiguous component endpoint makes the measurement unassessable; a reviewed
out-of-scope endpoint remains explicitly disclosed but is not a component
contributor. For footprint/type counts, every mapped contributing fact must
have its required owner/type identity. For public surface size, all governed
assemblies, selected facts, and source-of-truth inputs must resolve.

`0` is evaluable only after this proof. Missing input, unexpected empty
universe, stale metric target, ambiguous component, incomplete source set,
unmapped required endpoint, or unresolved owner follows #504/#505 as native
unassessable evidence. It cannot reduce a denominator or produce a clean zero.

Alternative considered: report the known subset with a warning. Rejected
because a later budget could compare that reduced subset as though it were the
whole declared architecture surface.

## Risks / Trade-offs

- [Topology is only partially declared] -> A component metric can still be
  useful where its own required relations are complete, but it becomes
  unassessable when an unreviewed unmapped or ambiguous endpoint would alter
  its cardinality.
- [Metric implementation duplicates an authority] -> The contract requires
  future work to consume canonical topology, graph, ownership, and public API
  facts rather than re-scan input.
- [Multi-targeting yields different values] -> Require one canonical analysis
  snapshot binding; never merge or arbitrarily choose target frameworks.
- [Users want more measures] -> Extend the catalog only through a reviewed
  capability/spec change with the same subject, universe, contributor, and
  applicability discipline.

## Migration Plan

This is a design-only change. Existing policies declare no metric controls and
remain unchanged. Dependent issues introduce any schema, implementation,
reporting, and budget migration incrementally; removing these artifacts has no
runtime rollback effect.
