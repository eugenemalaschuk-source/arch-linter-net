## Context

`architecture-metric-semantics` intentionally reuses existing graph and
topology facts. The dependency graph, however, has type, namespace, and
assembly levels only: external nodes exist at type/namespace but not assembly,
and no project-level graph exists. Native topology additionally permits project
subjects. The original design did not select a single authority or projection
for every combination.

## Goals / Non-Goals

**Goals:**

- Make one canonical fact source or bounded projection authoritative for every
  metric and native topology-subject combination.
- Permit project metrics without creating a project graph, source scan, or
  alternate direct-dependency meaning.
- Define exact unassessable and configuration-invalid outcomes when a required
  graph or canonical ownership binding cannot prove the result.

**Non-Goals:**

- Add a project graph, assembly external nodes, new topology selector, schema,
  evaluator, CLI, output, budget, or runtime behavior.
- Change the metric catalog, contributor cardinality, self-edge, cycle, or
  general applicability rules already specified by #516.

## Decisions

### 1. Preserve matching graph levels where they exist

Type and namespace component metrics use their matching direct graph level.
Assembly component metrics use the direct assembly graph, not a derived type
graph. This preserves the existing authority's intentional distinction between
direct code-level references and direct assembly metadata references.

Alternative considered: reduce every topology kind to type edges. Rejected
because it would redefine assembly metric semantics and lose direct assembly
references that the existing assembly graph intentionally represents.

### 2. Define project dependencies as a type-edge ownership projection

There is no canonical project dependency graph. For a project topology, each
direct first-party type edge is projected by replacing its endpoints with the
existing canonical owning project identities and then mapping those project
subjects to topology nodes. Component relations are the resulting distinct
node pairs. This is a projection over authoritative facts, not a new graph;
there is no source rescan, assembly-edge fallback, or transitive traversal.

Alternative considered: leave project component metrics unassessable. Rejected
because the existing type graph plus canonical ownership already expresses a
bounded, reproducible direct-project relation when bindings are complete.

### 3. Project and assembly external groups project type-level external edges

External edges exist only at type and namespace levels. Project and assembly
external-group metrics therefore project a canonical type-to-external-group
edge through the source type's canonical project or resolved assembly binding,
then map that owner to the topology node. The contributor remains the external
group identity and is set-deduplicated. Assembly-level external groups do not
reinterpret the assembly graph, which explicitly excludes external nodes.

Alternative considered: derive a synthetic external assembly graph. Rejected
because it would create a parallel graph authority and obscure that external
matching is based on type-level facts.

### 4. Fail closed on absent or non-one-to-one bindings

Every projection requires a complete graph and a unique canonical binding at
each projection step. A missing graph, missing or ambiguous owner, ambiguous
assembly graph-node binding, or unmapped/ambiguous required topology endpoint
makes the measurement unassessable. A metric that uses the wrong target shape
(such as type count on namespace topology) is configuration-invalid rather
than an unassessable neutral result.

## Risks / Trade-offs

- [Assembly graph identity cannot bind to one topology subject] -> Report
  unassessable instead of joining on a simple name or arbitrarily choosing a
  project.
- [Project projection is mistaken for a new graph] -> Require direct type-edge
  input, canonical owner replacement, set deduplication, and no independent
  project graph model.
- [External assembly count is confused with the assembly graph] -> State that
  it always begins with a type-level external edge because assembly graphs have
  no external nodes.

## Migration Plan

This is a design-only clarification. Existing policies have no metric controls,
so there is no runtime or policy migration. #517 implements the matrix when it
adds measure-first evaluation.
