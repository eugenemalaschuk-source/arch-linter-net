# Declared topology

`topology` is an opt-in, repository-local declaration of architecture components, allowed directions, and the bounded subject universe the declaration describes. It is a policy model, not a diagram language, runtime-service map, waiver, or automatic architecture design tool.

## A bounded topology

Declare `mode` as `partial` or `exhaustive`, exactly one observed `subject_kind` (`type`, `namespace`, `project`, or `assembly`), and a non-empty policy-owned `scope.selectors` list. The scope is required even for partial topology so a later evaluator never has to guess which observed subjects count.

```yaml
topology:
  mode: exhaustive
  subject_kind: type
  scope:
    allow_empty: false
    selectors:
      - layer: application
      - layer: domain
  nodes:
    - id: application
      mappings:
        - layer: application
    - id: domain
      mappings:
        - layer: domain
  allowed_edges:
    - from: application
      to: domain
  out_of_scope:
    - id: generated-proxies
      selector:
        namespace: MyProduct.Generated
      reason: Generated proxies are maintained outside the declared application topology.
  stale_declarations: true
```

The node `id` is the stable component identity, not display text. Edge endpoints always use node IDs. Nodes, mappings, edges, and reviewed scope entries retain policy provenance so reviewers can trace an entry to the authored root or imported fragment.

## Subject selectors

Each topology selector has exactly one primary selector. Its permitted kind is
defined by `subject_kind`, so the future evaluator never has to aggregate types
or infer ownership:

| `subject_kind` | Permitted selectors | Matching fact |
| --- | --- | --- |
| `type` | `layer`, `namespace`, `project`, `assembly`, `context` | The one observed type and its canonical namespace, project, assembly, and semantic facts. |
| `namespace` | `namespace`, `project`, `assembly` | The one observed namespace and its canonical owning project/assembly identity. |
| `project` | `project` | The one observed project identity. |
| `assembly` | `assembly` | The one observed assembly identity. |

`layer` and `context` are intentionally invalid for non-type topology: neither
means "any contained type" nor "all contained types." The closed vocabulary
otherwise reuses established policy semantics:

- `layer`: a declared layer key;
- `namespace` plus optional `namespace_suffix`: the existing literal or constrained whole-segment namespace glob grammar;
- `project`: an exact project identity;
- `assembly`: an exact assembly identity;
- `context`: the existing semantic role/metadata selector, including its documented CEL `when` predicate.

```yaml
nodes:
  - id: sales-domain
    mappings:
      - context:
          role: DomainLayer
          metadata:
            bounded_context: Sales
```

Topology adds no regular expressions, unconstrained wildcards, or new expression language. See [Layers and namespace patterns](layers-and-namespaces.md) and [Semantic classification](semantic-classification.md) for the reused selector rules.

## Partial and exhaustive modes

`partial` is the migration-friendly default: a scoped observed subject with no node mapping is not by itself an exhaustive-completeness failure. `exhaustive` makes a stronger claim: every observed subject in the declared kind and scope must resolve to exactly one component or an explicit reviewed out-of-scope declaration. With `allow_empty: false`, an empty observed universe is insufficient evidence rather than a clean result; set it to `true` only when an empty scope is deliberately valid.

The later declared-topology evaluator consumes these semantics. It reports native mapping evidence rather than inventing an implicit scope or treating zero findings as proof that topology was assessable.

## Mapping, reviewed scope, and drift

Mapping is set-based, never YAML-order based. Selector equality and ordering are
structural: metadata keys and values are compared as typed fields, while allowed
edges use their ordered `(from, to)` pair. Text containing punctuation such as
commas or `->` cannot merge distinct declarations. For one in-scope observed
subject, a matching `out_of_scope` declaration produces the reviewed
out-of-scope disposition; otherwise one matching node is mapped, zero is
unmapped, and multiple is ambiguous.

`out_of_scope` is intended-scope evidence, not baseline or waiver debt. Each entry needs a stable `id`, exactly one bounded selector, and a reviewable `reason`. Adding or broadening an entry can reduce declared governance scope, so the existing policy-weakening comparison retains it as typed comparison evidence.

Set `stale_declarations: true` to have the evaluator retain a distinct stale-declaration result for a node with no observed mapping or an allowed edge with no observed relationship. A stale node is not a new unmapped observed subject.

## Validation and boundaries

Policy loading rejects invalid selector shapes, unknown layer or node references, duplicate node IDs, duplicate directional edges, duplicate reviewed scope IDs, and exact duplicate mappings that make components unambiguously ambiguous. Fact-dependent selector overlap is preserved for the evaluator instead of being resolved by declaration order.

This schema does not parse PlantUML/Mermaid, scan dependencies, produce a topology score, create a topology-specific baseline or waiver, or modify a policy automatically. It is the native semantic source for later evaluation and optional diagram translation.
