## Context

The Core policy document is the repository-local semantic source for architecture
governance. It already has a provenance-aware YAML loader, a raw validator for
unknown or misplaced keys, a post-deserialization validator pipeline, typed
selectors for layers and semantic context, a policy-context export, and a
generic policy-weakening comparison. The shared applicability contract already
names `unmapped_subject`, `ambiguous_subject`, `stale_declaration`, and
`unexpected_empty_input`, but deliberately leaves each family’s native facts to
the family implementation.

Issue #508 must make topology declarative and reviewable without implementing
the observed dependency evaluator owned by #509, a diagram adapter owned by
#510, or the normalized projection owned by #507.

## Goals / Non-Goals

**Goals:**

- Add one opt-in `topology` section to the effective policy document.
- Give every topology node, edge, mapping, scope exclusion, and observed
  universe a deterministic, provenance-carrying identity.
- Reuse layer, namespace, project, assembly, and semantic-context selectors;
  do not add a general-purpose topology query language.
- State exactly how #509 classifies an in-scope subject as mapped, reviewed
  out of scope, unmapped, ambiguous, or stale.
- Preserve topology facts in policy context and make added/broadened reviewed
  scope exclusions visible through the existing generic weakening comparer.
- Leave policies without `topology` behaviorally unchanged.

**Non-Goals:**

- Scanning dependencies, resolving observed topology facts, emitting topology
  findings, producing applicability records, or changing CLI exits (#509,
  #506, and #507).
- Parsing PlantUML, Mermaid, or any external diagram syntax (#510).
- Adding a topology-only waiver, baseline, applicability, or weakening engine.
- Inferring node mappings, repairing policy, or treating a relation as a
  runtime-service topology.

## Decisions

### One topology model with an explicit subject universe

`ArchitectureContractDocument` receives an optional `Topology` model. A present
topology declares `mode: partial|exhaustive`, one `subject_kind`
(`type|namespace|project|assembly`), and a non-empty bounded `scope.selectors`
list. `scope.allow_empty` is explicit and defaults to false. Thus an evaluator
never chooses the observed granularity or silently substitutes its own scope.

Partial topology uses this scope to describe the evaluated universe but does
not treat absent node mappings as a completeness failure. Exhaustive topology
requires every subject in the universe to have exactly one disposition; an
empty observed universe is unassessable unless `allow_empty: true` is reviewed
in the policy.

### Reuse a closed selector vocabulary

`ArchitectureTopologySubjectSelector` permits exactly one primary selector:
`layer`, `namespace` (with the existing optional `namespace_suffix`),
`project`, `assembly`, or `context`. `context` reuses the existing
`ArchitectureContextSelector` (`role`, typed metadata, optional CEL `when`).
The namespace form uses the existing constrained namespace-glob grammar; no
wildcards, regular expressions, or free-form predicates are introduced beyond
the existing CEL selector contract. Validators reject empty, incompatible, or
multi-primary selector shapes.

This represents nodes, universe bounds, and exclusions using the same reviewable
policy concepts that the future evaluator already understands. It avoids a
second selector matcher while still supporting type-level topology selected by
layers, namespaces, projects, assemblies, or semantic roles.

### Stable declarations and set-based mapping

Nodes are an ordered YAML list with a required stable `id` and one-or-more
`mappings`; order is preserved for provenance only. `allowed_edges` contains
unique directional `(from, to)` node ID pairs. `out_of_scope` contains a stable
ID, one selector, and a non-empty reviewed reason. Every reference is validated
against declared nodes/layers as applicable, exact duplicate declarations are
rejected, and canonical comparisons sort by semantic identity.

For each canonical observed fact supplied by #509, classification is set-based:

1. it must first match the declared subject scope;
2. a matching reviewed out-of-scope declaration gives the explicit
   out-of-scope disposition;
3. otherwise, zero node matches is `unmapped`, one is `mapped`, and two or
   more is `ambiguous`.

YAML order must not alter this result. An exclusion is deliberately a scope
statement, not a waiver; it never produces automatic baseline debt. A selector
that is exact-duplicate across nodes is invalid at load time; fact-dependent
overlap is preserved for #509 as an ambiguous observed subject rather than
guessed during schema loading.

### Explicit declaration-drift switch

`stale_declarations` defaults to `false`. When enabled, #509 must report a
node as stale when its complete mapping set has no current subject and an edge
as stale when no current mapped dependency uses that directional pair. Stale
declaration evidence is separate from an unmapped subject and uses the existing
applicability reason code. Keeping it opt-in avoids turning forward-looking
partial topology declarations into migration failures.

### Context and weakening use existing seams

The policy-context export gains a typed optional topology projection containing
the exact effective mode, scope, nodes, mappings, edges, exclusions, reasons,
and provenance, and advances its schema version. The generic static-scope
comparison extends its current typed exception/scope work: adding a new
reviewed topology exclusion, or changing a same-ID exclusion to a broader
selector where direction is statically known, produces a normalized weakening
finding. It does not inspect YAML text, run topology evaluation, or create a
topology-specific comparer. Selector changes whose containment cannot be
proved remain existing bounded `impact_not_proven` evidence.

### Compatibility and validation placement

Raw validation protects topology’s closed YAML key shapes before
deserialization. A dedicated post-deserialization topology validator performs
cross-reference, duplicate, selector, and exhaustive-scope checks after
provenance is bound. The Core public-API snapshot is deliberately updated only
through the repository’s explicit reviewed snapshot lifecycle.

## Risks / Trade-offs

- **A broad exclusion could dilute exhaustive governance.** → The schema
  requires a non-empty bounded scope, named/reasoned exclusions, provenance,
  and generic weakening visibility; it provides no unreviewed catch-all.
- **Selector overlap cannot always be proven without observed facts.** → Reject
  exact duplicate declarations at load time and leave real subject-level
  ambiguity to #509 rather than using list order or unsafe static guesses.
- **Public policy-model types expand the reviewed Core API.** → Add focused
  API-drift verification and an explicit snapshot update as part of validation.
- **Context schema changes affect comparison consumers.** → Keep topology
  optional, preserve no-topology exports’ policy behavior, version the export,
  and cover deterministic context/weakening tests.
