# Semantic Classification (Partially Implemented)

**`classification.attributes` and `classification.assembly_attributes` are implemented**
(see [issue #109](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/109) and
`openspec/specs/attribute-role-extraction`): type-level and assembly-level attributes
mapped by full type name are extracted and canonicalized into role/metadata facts,
with `type_attribute` precedence over `assembly_attribute`. **Every other part of
this page — `precedence` beyond these two sources, `inheritance`, `namespace`, `path`,
`overrides`, `exclusions`, and `layers.<name>.selector` — remains reserved by the
YAML schema only.** A policy declaring those sections today is schema-valid, but
they have **no effect** on validation — no role is assigned from them, no selector
ever matches, and no diagnostic is produced from them. This page documents the
reviewed shape so policy authors and AI agents do not treat the unimplemented parts
as a working feature before their own implementation issues land. See
[Supported capabilities and non-goals](supported-capabilities.md) for the
authoritative list of what is enforced today.

**This capability produces facts, not contract results.** Extraction records role,
metadata, and `conflict`/evidence-extraction-failure facts on an in-memory result —
it is not yet wired into any `strict_*`/`audit_*` contract family's pass/fail
evaluation, CLI output, or SARIF diagnostics. A future issue is expected to surface
these facts through the diagnostics pipeline.

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

- Extraction: **implemented** for `attributes`/`assembly_attributes` (type-level
  and assembly-level attributes, matched by full type name). Inheritance facts
  and namespace/path conventions are still never read from scanned code.
- Role assignment: **implemented** for the `type_attribute`/`assembly_attribute`
  sources, including `type_attribute` precedence over `assembly_attribute` and
  the `classification.precedence` subset that enables/disables these two
  sources. No other source (`yaml_override`, `inheritance`, `namespace`,
  `path`) ever assigns a role yet.
- No selector matching: `layers.<name>.selector` never selects any type, even
  though `classification.attributes`/`assembly_attributes` now produce real
  role/metadata facts a future selector-matching engine could consume.
- Facts, not surfaced diagnostics: `conflict` and evidence-extraction-failure
  facts are recorded on the in-memory extraction result for the implemented
  sources, but nothing wires them into CLI output, SARIF, or any contract
  family's pass/fail evaluation yet. `stale selector` and `uncovered semantic fact` remain vocabulary only.
- No annotation package: this design does not ship, and does not require, a
  binary ArchLinterNet annotation assembly — see
  [Annotation strategy](#annotation-strategy) below for the full adoption
  decision.
- No coverage integration: the planned `scope: semantic_role` coverage variant
  (tracked by a follow-up issue) does not exist yet.

## Annotation strategy

**Decision ([issue #108](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/108)):**
ArchLinterNet ships no binary annotation package and no source-only
annotation package. **User-defined attributes mapped by full type name in
YAML are the sole supported adoption path.** A project that wants
`classification.attributes` evidence defines its own attribute — commonly a
handful of lines, as in the [reviewed shape](#reviewed-shape) example above —
and maps it by full type name. No package reference, and no dependency on any
ArchLinterNet-provided assembly, is required or offered.

This is a first-wave decision, not a permanent restriction: an optional
package remains possible as a future, separately-decided convenience if
concrete adoption need emerges. It is not the default path today.

### Trade-offs considered

| Path | Dependency footprint | Setup cost | Versioning | Status |
|---|---|---|---|---|
| User-owned attribute | None — the attribute lives in the adopting project's own code | One small attribute class, written once | No annotation-package version coupling; YAML and extraction compatibility still follow ArchLinterNet's own compatibility policy | **Recommended adoption path** — runtime extraction implemented for `attributes`/`assembly_attributes` ([Current limits](#current-limits)) |
| Source-only annotation package | No runtime assembly reference, but still a versioned artifact ArchLinterNet must design, ship, and support | Add a package reference; attribute ships pre-written | Coupled to ArchLinterNet's release/compatibility policy | Not shipped; possible future convenience |
| Binary annotation package | Adds a compile-time (and possibly runtime/dependency-graph) reference to `ArchLinterNet.Annotations` in every consuming project | Add a package reference; attribute ships pre-written | Coupled to ArchLinterNet's release/compatibility policy | Not shipped; explicitly ruled out as a default or required path |

A binary package was rejected as the default because it would add exactly the
kind of mandatory runtime/dependency-graph reference ArchLinterNet's
non-invasive positioning is meant to avoid. A source-only package has not been
shipped, since no consumer need has been demonstrated — adding one now would
be speculative packaging work with no current user. User-owned attributes need
neither: the reviewed mapping shape in [Reviewed shape](#reviewed-shape)
accepts any full attribute type name, regardless of which assembly declares
it, and — for `attributes`/`assembly_attributes` — is now actually evaluated
against scanned code; see [Current limits](#current-limits) for what remains
unimplemented.

## Where to look next

- [Supported capabilities and non-goals](supported-capabilities.md)
- [Layers and namespace patterns](layers-and-namespaces.md)
- [Coverage contracts](../contracts/coverage.md)
