# Semantic Classification

Semantic classification lets ArchLinterNet derive a single architectural role plus metadata for each analyzed type and use those facts in selectors, contextual contracts, diagnostics, and semantic-role coverage.

This page distinguishes implemented behavior from schema-reserved/deferred fields. The machine-readable capability boundary is `archlinternet.capabilities.json`; runtime validators and the packaged schema remain authoritative.

## Support status

| Feature | Status | Notes |
| --- | --- | --- |
| `classification.attributes` | Implemented | Maps type-level attributes by full type name to role/metadata facts. |
| `classification.assembly_attributes` | Implemented | Maps assembly-level attributes to role/metadata facts. |
| `classification.inheritance` | Implemented | Matches configured base classes/interfaces transitively. |
| `classification.namespace` | Implemented | Uses the documented namespace/glob/suffix matching model. |
| `layers.<name>.selector` | Implemented | Supports selector-only layers or namespace + selector with AND semantics. |
| Contextual dependency / allow-only selectors | Implemented | Match role/metadata directly without declaring a layer. |
| Semantic port-boundary selectors | Implemented | Govern selected context crossings through a port/ACL seam. |
| `scope: semantic_role` coverage | Implemented | Reports unclassified, ungoverned, stale, conflicting, or failed semantic evidence. |
| `classification.path` | Deferred | Accepted only where documented; produces no role assignment. |
| `classification.overrides` / `classification.exclusions` | Deferred | Schema-reserved; do not change runtime classification today. |

The effective precedence of implemented classification facts is:

```text
type_attribute > assembly_attribute > inheritance > namespace
```

Deferred sources do not contribute role facts merely because their YAML shape exists.

## Basic example

```yaml
classification:
  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        domain: constructor[0]

  assembly_attributes:
    - attribute: Acme.Architecture.BoundedContextAttribute
      role: ApplicationLayer
      metadata:
        boundedContext: constructor[0]

  inheritance:
    - base_type: Acme.Domain.AggregateRootBase
      role: AggregateRoot
      metadata:
        domain: Sales

  namespace:
    - namespace: MyApp.Sales.Domain
      role: DomainLayer
      metadata:
        domain: Sales

layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales

  sales_application:
    namespace: MyApp.Sales
    selector:
      role: ApplicationLayer
```

`namespace` is optional when `selector` is present. When both are declared, a type must satisfy both predicates.

Existing namespace-only layers continue to work unchanged.

## Classification output

Role resolution is performed once per validation run. JSON/CI output exposes the effective role index together with classification conflicts and metadata-extraction failures.

A classified entry contains the subject, role, source mechanism, evidence, and canonical metadata, for example:

```json
{
  "subject": "MyApp.Sales.Order",
  "role": "DomainLayer",
  "source": "TypeAttribute",
  "evidence": "Acme.Architecture.DomainLayerAttribute",
  "metadata": { "domain": "Sales" }
}
```

The classification index is input to selector-backed layers and contextual/semantic contract families; it is not merely informational output.

## Metadata extraction syntax

Attribute/assembly-attribute mappings support four bounded metadata forms:

| Form | Meaning |
| --- | --- |
| `constructor[<index>]` | Positional constructor argument from the statically resolved attribute usage. |
| `property:<Name>` | Named argument explicitly present in that attribute usage. |
| `const:<Full.Type.NAME>` | Compile-time `const` field resolved statically. |
| any other scalar | Literal YAML scalar. |

Inheritance and namespace mappings have no attribute constructor/property evidence, so use literal or supported static values there.

Metadata is canonicalized into comparable string, boolean, or numeric domains. CLR type values are represented by full type name; enum values use their unambiguous member name. Unsupported/ambiguous values, unresolved references, missing constructor arguments, and missing named arguments are recorded as extraction failures rather than guessed.

An extraction failure does **not** fabricate metadata and does not erase an otherwise valid role assignment. The affected metadata key is omitted and the failure remains visible in diagnostics/evidence.

`const:` is deliberately static-analysis-only: it never evaluates `static readonly` initializers or executes user code.

## `layers.<name>.selector`

A layer selector matches the resolved role plus optional metadata constraints:

```yaml
layers:
  sales_commands:
    selector:
      role: Command
      metadata:
        boundedContext: Sales
```

Rules:

- `role` is exact-match;
- metadata constraints are exact and AND-combined;
- selector-only layers are valid;
- namespace + selector is an intersection (AND), not a union;
- wildcard/regex matching is not implied by ordinary layer selector metadata;
- an empty non-external selector-only layer is surfaced as a configuration/coverage concern rather than silently widening to everything.

A selector-backed layer can be referenced by the ordinary dependency, allow-only, layer-order, cycle, independence, protected-surface, and other layer-based families just like a namespace-backed layer.

## Contextual selectors (`context_dependencies`, `context_allow_only`)

Contextual dependency/allow-only contracts compare semantic facts directly. They do not require an intermediate `layers.<name>` declaration.

Example:

```yaml
contracts:
  strict_context_dependencies:
    - id: no-cross-domain-dependency
      name: no-cross-domain-dependency
      source:
        role: DomainLayer
      forbidden:
        - role: DomainLayer
          metadata:
            domain: "!{source.metadata.domain}"
      reason: Domain types must not cross bounded-context boundaries directly.
```

Contextual metadata values use a closed operator vocabulary:

| Form | Operator | Meaning |
| --- | --- | --- |
| YAML sequence | `in` | Candidate value equals any listed value. |
| `"*"` | `any` | Any resolved value, provided the key exists. |
| `"!{source.metadata.<key>}"` | `not-equal-to-source` | Candidate value differs from the current source type's value. |
| other scalar | `exact` | Exact canonical value. |

`source`, target (`forbidden`/`allowed`), and exclusions may also use the documented CEL `when` predicate locations. CEL is additive to literal role/metadata constraints and is evaluated under the closed context documented in [CEL policy expressions](cel-expressions.md).

Per-edge `dependency.*` CEL facts remain reserved until the runtime supplies real edge data. A policy must not infer availability from schema shape alone.

Use a named layer selector when a stable semantic group is reused by many ordinary contracts. Use a contextual selector when the rule is naturally about role/metadata relationships between the current source and target.

## Semantic port boundaries

Port-boundary contracts use the same semantic evidence to require selected cross-context dependencies to pass through an explicit port/anti-corruption seam. This is distinct from simply forbidding a dependency.

See [Semantic port boundary contracts](../contracts/port-boundary.md).

## Semantic-role coverage

`scope: semantic_role` is implemented and is the guardrail that prevents semantic governance from silently becoming partial.

```yaml
contracts:
  strict_coverage:
    - id: semantic-role-coverage
      name: semantic-role-coverage
      scope: semantic_role
      exclude:
        - role: GeneratedRole
          reason: Generated types are governed outside this policy.
      reason: Every discovered semantic fact must be intentionally governed.
```

A semantic fact is governed when it is consumed by a matching selector-backed layer or an implemented contextual/semantic contract selector. Coverage can report, among other evidence:

- first-party types with no resolved role;
- classified facts with no governing selector;
- valid selectors that currently match no classified type;
- classification conflicts;
- metadata extraction failures.

This is additive to `namespace`, `project`, `assembly`, `dependency_edge`, and `rule_input` coverage. See [Coverage contracts](../contracts/coverage.md).

## Precedence

When several implemented sources classify the same type, the higher-precedence implemented source wins:

1. type attribute;
1. assembly attribute;
1. inheritance;
1. namespace.

The winning source contributes the role and its metadata; roles from lower-precedence sources are not accumulated as tags. A type therefore has one effective role in the current model.

Do not model orthogonal concerns by inventing multiple simultaneous roles. Use metadata, namespace/layer membership, public-API surface selectors, or another purpose-built contract when the concern is independent of the primary semantic role.

## Annotation strategy

ArchLinterNet does not require or ship a mandatory architecture-annotation package. Repositories define their own attributes and map them by full type name in YAML:

```csharp
[DomainLayer("Sales")]
public sealed class Order { }
```

```yaml
classification:
  attributes:
    - attribute: MyCompany.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        domain: constructor[0]
```

This keeps ArchLinterNet decoupled from application binaries. Attributes are one evidence source, not a requirement: inheritance and namespace mappings remain supported alternatives.

The [Semantic role catalog](semantic-role-catalog.md) is vocabulary guidance, not a list of framework types that ArchLinterNet automatically injects or discovers by name.

## Current limits

The current semantic model is intentionally bounded:

- one winning role per type; no accumulated multi-role/tag model;
- exact layer-selector role/metadata matching; contextual selectors have only the documented closed operators;
- no arbitrary regex or scripting language in selector values;
- `classification.path` does not assign roles;
- schema-reserved `overrides`/`exclusions` do not currently alter role resolution;
- no runtime DI graph, authorization decision, ownership, or arbitrary data-flow inference;
- CEL works only at documented locations and cannot be used to invent new fact sources;
- reserved per-edge dependency CEL facts must not be treated as populated runtime evidence.

If a field is described here as deferred, use an implemented source/contract instead of relying on silent no-op behavior.

## Choosing between namespace and semantics

Prefer namespace-backed layers when repository layout already expresses the boundary clearly and stably. Prefer semantic classification when the architecture decision is expressed by code facts such as an attribute, inheritance/interface relationship, or role/metadata context that cuts across namespaces.

The two models can be combined: `namespace + selector` narrows a semantic role to a specific structural area while preserving deterministic layer-based contracts.
