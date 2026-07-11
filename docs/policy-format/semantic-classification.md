# Semantic Classification (Partially Implemented)

**`classification.attributes`, `classification.assembly_attributes`, and
`layers.<name>.selector` are implemented**
(see [issue #109](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/109) and
`openspec/specs/attribute-role-extraction`): type-level and assembly-level attributes
mapped by full type name are extracted and canonicalized into role/metadata facts,
with `type_attribute` precedence over `assembly_attribute`. Selector-backed layers
resolve types by exact role/metadata match and produce violations through existing
contract families (dependency, layer-order, allow-only, etc.) exactly as
namespace-based layers do. Empty non-external selector-only layers surface as
configuration diagnostics. **Every other part of this page — `precedence` beyond
`type_attribute`/`assembly_attribute`, `inheritance`, `namespace`, `path`,
`overrides`, and `exclusions` — remain reserved by the YAML schema only.** A policy
declaring those sections today is schema-valid, but they have **no effect** on
validation — no role is assigned from them and no diagnostic is produced from them.
This page documents the reviewed shape so policy authors and AI agents do not treat
the unimplemented parts as a working feature before their own implementation issues
land. See [Supported capabilities and non-goals](supported-capabilities.md) for the
authoritative list of what is enforced today.

**Attribute-based classification produces facts consumed by selector-backed
layers.** Extraction records role, metadata, and `conflict`/evidence-extraction-failure
facts. Selector-backed layers consume the role index to match types, and contract
violations from those layers affect pass/fail evaluation and the command's exit code.
`validate` also surfaces classification facts in human, JSON, and CI-artifact output
as informational "Classification findings."

**Discovered roles are indexed once per validation run** (see
[issue #110](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/110)
and `openspec/specs/semantic-role-index`): every scanned type's resolved role,
metadata, classification source, and evidence is computed in a single pass and
cached for the run, rather than recomputed on every lookup. `validate --format json`/CI-artifact output includes a `classification_roles` array alongside
`classification_conflicts`/`classification_metadata_failures`, one entry per
classified type:

```json
{
  "classification_roles": [
    {
      "subject": "MyApp.Sales.Order",
      "role": "DomainLayer",
      "source": "TypeAttribute",
      "evidence": "Acme.Architecture.DomainLayerAttribute",
      "metadata": { "domain": "Sales" }
    }
  ]
}
```

`source` names the classification *mechanism* (`TypeAttribute` or
`AssemblyAttribute`); `evidence` names the specific attribute type whose
mapping produced the role, so the two together answer both "how" and
"which fact" a role assignment came from. `evidence` is `null` when no role
was resolved. This index is consumed by selector-backed layer resolution;
classification findings are also surfaced as informational output in
human/JSON/CI-artifact formats.

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
    namespace: MyApp.Sales.Domain   # optional when selector is present
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

`selector` is an optional exact-match selector. `selector.role` matches the
resolved role string and `selector.metadata` is an optional set of exact-match,
AND-combined key/value constraints. A layer may declare only `selector`, only
`namespace`, or both; when both are present, both constraints must match. No
wildcard or regex value matching is supported. Declaring only `namespace` (as
every layer did before this feature) remains unaffected.

## Contextual selectors (`context_dependencies`, `context_allow_only`)

The `strict_context_dependencies`/`audit_context_dependencies` and
`strict_context_allow_only`/`audit_context_allow_only` contract families (see
[Contextual dependency contracts](../contracts/context-dependency.md) and
[Contextual allow-only contracts](../contracts/context-allow-only.md)) compare
discovered role/metadata directly between a `source` selector and
`forbidden`/`allowed`/`exclude` selectors, without an intermediate
`layers.<name>` declaration. Their selector shape (`role` + `metadata`) looks
like `layers.<name>.selector` but supports a broader, closed operator
vocabulary instead of exact/AND-only matching:

| Form | Operator | Meaning |
|---|---|---|
| YAML sequence | `in` | Matches if the type's resolved value equals any listed entry. |
| `"*"` | `any` | Matches any resolved value, provided the key is present. |
| `"!{source.metadata.<key>}"` | `not-equal-to-source` | Matches when the candidate's resolved value for `<key>` differs from the *current match's source type's own* resolved value for `<key>`. Only valid on `forbidden`/`allowed`/`exclude` — a `source` selector has no other source to reference. |
| anything else | `exact` | Literal scalar match (same string/boolean/decimal cross-representation equality as `layers.<name>.selector`). |

These four forms are checked in the fixed order above and are the only forms
supported — no regex or open-ended expression syntax.

### Comparison with `layers.<name>.selector`

| | `layers.<name>.selector` | Contextual selector (`source`/`forbidden`/`allowed`/`exclude`) |
|---|---|---|
| Matching | Exact literal, AND-combined across declared keys. | Exact, `in`, `any`, or `not-equal-to-source`, per metadata key. |
| Cross-referencing another type's own metadata | Not supported. | `not-equal-to-source` compares against the *current source type's* resolved metadata. |
| Requires a declared `layers.<name>`? | Yes — a layer is the unit contracts reference. | No — selectors are declared inline on the contextual contract itself. |
| Used by | Every existing contract family (`dependency`, `allow_only`, `layers`, `cycles`, etc.) via a named layer. | Only `context_dependencies`/`context_allow_only`, referenced inline. |
| Coverage-participating consumption | Yes. | Yes — a contextual selector's `(role, metadata key)` reference is registered as coverage-participating consumption identically to a `layers.<name>.selector` match. |

Use `layers.<name>.selector` when the boundary is a small, fixed, named set of
layers referenced by many contracts. Use a contextual selector when the
boundary is a business-context distinction (e.g. "no domain type in one
bounded context may depend on a domain type in another") that would otherwise
require enumerating every concrete layer pair, or when `not-equal-to-source`'s
cross-referencing comparison is what the rule actually needs to express.

## Current limits

- Extraction: **implemented** for `attributes`/`assembly_attributes` (type-level
  and assembly-level attributes, matched by full type name). Inheritance facts
  and namespace/path conventions are still never read from scanned code.
- Role assignment: **implemented** for the `type_attribute`/`assembly_attribute`
  sources, including `type_attribute` precedence over `assembly_attribute` and
  the `classification.precedence` subset that enables/disables these two
  sources. No other source (`yaml_override`, `inheritance`, `namespace`,
  `path`) ever assigns a role yet.
- Selector matching: **implemented** — uses the per-run role index and exact
  role/metadata predicates; a layer may declare only `selector`, only
  `namespace`, or both. Empty non-external selector matches are surfaced as
  configuration diagnostics. Selector-backed layers produce violations through
  existing contract families exactly as namespace-based layers do.
- Role index: **implemented** as a per-run, lazily-computed cache
  (`ArchitectureRoleIndex`) of every classified type's role/metadata/source/
  evidence, surfaced as `classification_roles` in JSON/CI-artifact output. It
  is consumed by selector-backed layer resolution. `stale selector` and
  `uncovered semantic fact` remain vocabulary only.
- Classification findings (roles, conflicts, metadata failures) are surfaced
  as informational output in human, JSON, and CI-artifact formats. They are not
  wired into SARIF diagnostics.
- No coverage integration: the planned `scope: semantic_role` coverage variant
  (tracked by a follow-up issue) does not exist yet.
- No annotation package: this design does not ship, and does not require, a
  binary ArchLinterNet annotation assembly — see
  [Annotation strategy](#annotation-strategy) below for the full adoption
  decision.

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
