# Semantic Classification (Reserved)

**Schema-accepted, not yet enforced.** `classification` and
`layers.<name>.selector` are **reserved by the YAML schema only**. No
extraction, role-assignment, or selector-matching engine exists yet. A policy
that declares `classification` or `selector` today is schema-valid, but the
section has **no effect** on validation — no role is ever assigned, no
selector ever matches, and no diagnostic is ever produced from it. This page
documents the reviewed shape so policy authors and AI agents do not treat it as
a working feature before implementation lands. See
[Supported capabilities and non-goals](supported-capabilities.md) for the
authoritative list of what is enforced today.

## Why this section exists

ArchLinterNet's `layers` map namespaces to layer names by glob pattern only. The
semantic classification design ([issue #107](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/107))
reviews a YAML shape that will let code itself — attributes, assembly metadata,
inheritance, and namespace/path conventions — imply an architectural role and
metadata, so a layer can eventually be selected by discovered role instead of
(or in addition to) namespace. The full design record lives at
`openspec/changes/archive/2026-07-10-design-semantic-classification-model/design.md`
in the repository.

## Reviewed shape

```yaml
classification:
  precedence:            # optional; a non-empty, ordered, duplicate-free
                          # subsequence of the six fixed sources below.
                          # default: all six, in this order.
    - yaml_override
    - type_attribute
    - assembly_attribute
    - inheritance
    - namespace
    - path

  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute   # full type name
      role: DomainLayer
      metadata:
        domain: constructor[0]        # positional constructor argument
        module: property:Module       # named property/field
        tier: const:Acme.Architecture.Tiers.CORE   # compile-time const only
        owner: platform-team          # literal scalar

  assembly_attributes:
    - attribute: Acme.Architecture.BoundedContextAttribute
      role: ApplicationLayer
      metadata:
        boundedContext: constructor[0]

  inheritance:
    - base_type: Acme.Domain.AggregateRootBase
      role: DomainLayer
      metadata:
        domain: Sales                 # literal only - no ctor/property evidence

  namespace:
    - namespace: MyApp.Sales.Domain
      role: DomainLayer
      metadata:
        domain: Sales
    - namespace_suffix: Repositories
      role: Repository

  path:
    - path_prefix: src/Sales/Domain
      role: DomainLayer
      metadata:
        domain: Sales

  overrides:
    - type: MyApp.Legacy.OrderProcessor    # narrow: reason optional
      role: ApplicationLayer
    - namespace: MyApp.Legacy               # broad: reason required
      role: Unclassified
      reason: Legacy area predates attribute adoption; reviewed quarterly.

  exclusions:
    - namespace_suffix: Generated
      reason: Source-generated code is not hand-authored and is exempt from classification.

layers:
  domain:
    namespace: MyApp.Sales.Domain   # required - selector is additive, not a substitute
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
  infrastructure:              # existing namespace-only layers are unaffected
    namespace: MyApp.Infrastructure
```

## Precedence

Source precedence is fixed, highest first:

1. `yaml_override`
1. `type_attribute`
1. `assembly_attribute`
1. `inheritance`
1. `namespace`
1. `path`

`classification.precedence`, when declared, must be a subsequence of this
order — the schema rejects a reordered list (e.g. `[namespace, type_attribute]`),
a repeated entry (e.g. `[namespace, namespace]`), and an empty list. Sources the
list omits are disabled; omit `precedence` entirely to use all six sources in
the fixed order.

## Metadata extraction syntax

Each `metadata.<key>` value is interpreted by exactly one of four fixed forms,
checked in this order:

| Form | Meaning |
|---|---|
| `constructor[<index>]` | The attribute's zero-indexed positional constructor argument, from the fully compiler-resolved argument list (including substituted defaults for omitted optional parameters). Not applicable to `inheritance`/`namespace`/`path`, which carry no constructor evidence. |
| `property:<Name>` | A named argument called `<Name>` explicitly present in that specific attribute usage's own recorded metadata — never a property/field the attribute *type* merely declares. `attributes`/`assembly_attributes` only. |
| `const:<Full.Type.NAME>` | The value of a **compile-time `const` field**, resolved statically by full type-qualified name. |
| anything else | A literal YAML scalar, used verbatim. |

`const:` deliberately resolves **only compile-time `const` fields**, never
`static readonly`. A `static readonly` field's initializer can run arbitrary
code — a method call, an environment-variable read, I/O, or any other
runtime-computed expression — and evaluating it would require either executing
that code (which a static-analysis-only tool must not do) or a much narrower
literal-initializer detection that does not exist yet.

`property:<Name>` reads only what a specific attribute usage's own metadata
records as an explicitly-supplied named argument — it never falls back to a
property's declared default or initializer value. `[Domain("Sales")]` against
`DomainAttribute { public string? Module { get; set; } }` records zero named
arguments even though `Module` exists as a settable property: reading
`Module`'s default would require instantiating the attribute and running its
initializer, which the static-analysis-only boundary forbids. A property that
exists on the type but was not supplied in that usage is therefore the same
extraction failure as a property that does not exist at all.

**Metadata values are canonicalized into exactly three comparable domains
before matching**: **string** (CLR `string`; `System.Type` values as
`Type.FullName`; enum values as their declared member name, not the
underlying integer — but only when the underlying value maps to exactly one
declared member; an aliased enum value, e.g. `enum Tier { Core = 1, Domain = 1 }`, has no single correct name to canonicalize to and is an extraction
failure rather than a guess); **boolean**; and **decimal** (every CLR numeric
primitive — `byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`/
`float`/`double`/`decimal` — and every YAML/JSON numeric literal), so a CLR
`int` `1` and a YAML `1.0` canonicalize to the same value and compare equal.
A `const decimal` field canonicalizes trivially, since `decimal` is already
the canonical domain's own representation. Arrays, other attribute-typed
values, `null`, unmapped/aliased enum values, and non-`decimal`-representable
numbers (`NaN`, `Infinity`) have no representation in any of the three
domains and are an extraction failure, the same as an unresolved reference.

**Evidence-extraction failure is uniform across all three evidence-referencing
forms and every canonicalization failure above, and never blocks role
assignment.** An out-of-range `constructor[N]`, a missing or unsupplied
`property:Name`, an unresolved `const:` reference, or an unsupported value
shape all resolve the same way: that metadata key is **omitted** from the
type's assigned metadata — not fabricated, not defaulted — and the type still
receives its role from the matching source, since role assignment does not
depend on every metadata key resolving. Every extraction failure is recorded
as an explainable fact so a policy author can see why an expected metadata key
is missing, rather than the omission looking like an unrelated authoring
mistake.

**Repeated instances of one mapped attribute** (a repeatable custom attribute
applied more than once, e.g. `[Domain("Sales")] [Domain("Inventory")]`)
resolve by the attributes' metadata order (first instance wins) rather than by
YAML declaration order, since there is only one `classification.attributes`
entry mapping `Domain` in that example, not two. Identical repeated instances
(same role, same metadata) are not treated as a conflict.

## Overrides and exclusions

Every `overrides`/`exclusions` entry declares **exactly one** scope field —
`type`, `namespace`, or `namespace_suffix`. Combining more than one scope field
on a single entry is rejected by the schema; author two separate entries
instead.

- A `type`-scoped override is narrow (the type name is the scope) and does not
  require `reason`.
- A `namespace`/`namespace_suffix`-scoped override is broad (affects every type
  currently or later matching that pattern) and **requires** `reason`.
- Every `exclusions` entry requires `reason`, regardless of scope.

**An override does not by itself exempt a type from coverage.** `overrides` is
a classification *source* — the highest-precedence one — not a
coverage-exemption mechanism. A type whose role came from an override still
needs a `selector` (or a future contextual contract) to actually consume that
fact, or it remains an `uncovered semantic fact` exactly like a role assigned
by any other source. Only `exclusions` removes a type from coverage
consideration entirely.

## `layers.<name>.selector`

`namespace` remains **required** on every layer — `selector` is additive
alongside it, never a substitute for it. A namespace-less, selector-only
layer would carry an empty `Namespace` into
`ArchitectureLayerResolver.IsProjectType`'s unconditional `GlobPattern` access
on every declared layer and crash with `InvalidNamespacePatternException` at
real execution time, not just at schema-validation or YAML-load time.
Selector-only layers are deferred to #111, which must implement the resolver
changes an empty-namespace layer requires. `selector.role` is an exact-match
string against whatever role names `classification.attributes`/
`inheritance`/`namespace`/`path`/`overrides` declare (there is no fixed role
catalog enforced by the schema). `selector.metadata` is an optional set of
exact-match, AND-combined key/value constraints — no wildcard or regex value
matching. Declaring only `namespace` (as every layer did before this design)
is unaffected.

## Current limits

- No extraction: attributes, assembly attributes, inheritance facts, and
  namespace/path conventions are never read from scanned code.
- No role assignment: no type ever receives a role or metadata value.
- No selector matching: `layers.<name>.selector` never selects any type.
- No diagnostics: `conflict`, `stale selector`, and `uncovered semantic fact`
  are vocabulary only — nothing is ever reported.
- No annotation package: this design does not ship, and does not require, a
  binary ArchLinterNet annotation assembly.
- No coverage integration: the planned `scope: semantic_role` coverage variant
  (tracked by a follow-up issue) does not exist yet.

## Where to look next

- [Supported capabilities and non-goals](supported-capabilities.md)
- [Layers and namespace patterns](layers-and-namespaces.md)
- [Coverage contracts](../contracts/coverage.md)
