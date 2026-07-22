# Layers, Namespace Patterns, and Semantic Selectors

Layers are named architecture surfaces used by contracts.

```yaml
layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
```

Contract rules reference layer keys such as `application` and `domain`.

## Semantic selectors

Layers may select classified types by exact role and metadata. A selector-only
layer is valid, and a layer that declares both `namespace` and `selector` uses
both constraints (logical AND):

```yaml
layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
  domain:
    namespace: MyApp.Domain
    selector:
      role: DomainLayer
```

Metadata keys and values are matched exactly; multiple metadata entries must all
match. Selectors do not support wildcard or regular-expression values. A valid
selector that matches no loaded types is reported as an empty selector unless
the layer is marked `external: true`.

## Selector `when` predicates

`layers.<name>.selector` accepts an optional `when` field: a CEL predicate
that refines the selector's literal `role`/`metadata` match. `when` is
additive — a type belongs to the layer only if it already matches `role` and
`metadata`, *and* `when` evaluates to `true`:

```yaml
layers:
  sales_domain:
    selector:
      role: Domain
      when: subject.metadataText["domain"] == "Sales"
```

`when` compiles against a fixed `subject` context exposing identity facts
(`fullName`, `simpleName`, `namespace`, `assemblyName`, `projectName`),
classification facts (`role`, `metadataText: Map[String]`,
`metadataBool: Map[Bool]`), type facts (`kind`, `isAbstract`, `isSealed`,
`baseTypeNames`, `interfaceTypeNames`, `attributeTypeNames`), and path facts
(`sourcePaths`, `sourceDirectoryPrefixes`). Numeric metadata is not exposed to
`when` — match it with a literal `metadata` constraint instead.

Important boundary:

- ordinary selector fields such as `role`, `metadata`, `namespace`, and
  `namespace_suffix` are always literal values, never implicitly parsed as
  expressions — only the explicit `when` field carries a CEL predicate;
- a selector with no `when` behaves exactly as before this field existed —
  `when` never runs unless declared;
- `when` evaluating to `false` is an ordinary non-match; a `when` evaluation
  failure (e.g. an unguarded reference to an absent `metadataText` key) fails
  the run as a policy/configuration error, not a silent non-match, and is
  never suppressed by baseline;
- a selector whose combined literal-and-`when` match set is empty is reported
  as an empty/stale selector the same way a purely literal empty selector is.

## Literal namespace prefixes

A literal `namespace` value matches the exact namespace and child namespaces:

```yaml
layers:
  domain:
    namespace: MyApp.Domain
```

This matches `MyApp.Domain`, `MyApp.Domain.Models`, and deeper descendants. It does not match unrelated prefixes such as `MyApp.DomainLegacy`.

## Constrained namespace globs

`namespace` supports a constrained `*` wildcard when it occupies a complete namespace segment:

```yaml
layers:
  feature_modules:
    namespace: MyApp.Features.*
```

Rules:

- `*` matches exactly one namespace segment.
- Descendants under the resolved prefix also match.
- `*` must be a full segment.
- Multi-segment wildcards, partial-segment wildcards, character classes, and regular-expression syntax are not supported.
- Leading wildcard patterns are not supported.

Examples:

| Pattern | Namespace | Matches? |
|---------|-----------|----------|
| `MyApp.Features.*` | `MyApp.Features.Audio` | yes |
| `MyApp.Features.*` | `MyApp.Features.Audio.Player` | yes |
| `MyApp.Features.*` | `MyApp.Features` | no |
| `MyApp.Features.*` | `MyApp.Other.Audio` | no |

## Namespace suffix

Use `namespace_suffix` to model conventions such as `Contracts`, `Models`, or `Api` slices:

```yaml
layers:
  feature_contracts:
    namespace: MyApp.Features.*
    namespace_suffix: Contracts
```

With glob patterns, the suffix is position-fixed immediately after the resolved wildcard segment.

Matches:

- `MyApp.Features.Audio.Contracts`
- `MyApp.Features.Audio.Contracts.Dto`

Does not match:

- `MyApp.Features.Audio.Internal.Contracts`

## Excluding namespaces from a layer

A layer may declare `exclude`: a list of `namespace`/`namespace_suffix` entries
subtracted from the layer's matched scope. A namespace belongs to the layer
only if it matches `namespace`/`namespace_suffix` **and** matches none of the
`exclude` entries — `result = include - union(excludes)`.

```yaml
layers:
  modules_core:
    namespace: Product.Modules.*
    exclude:
      - namespace: Product.Modules.*.Infrastructure
      - namespace: Product.Modules.*.Persistence
```

This matches every namespace under `Product.Modules.*` except
`Product.Modules.<Module>.Infrastructure` and `Product.Modules.<Module>.Persistence`
(and their descendants). Every contract family that references a layer by
name — dependency, allow-only, external-dependency, protected, cycle, and
acyclic-sibling contracts — observes the narrowed scope automatically, with
no exclusion configuration of its own.

`exclude` entries use exactly the same namespace glob grammar as `namespace`
and `namespace_suffix` above (whole-segment `*` wildcards, no `**`/`?`/character
classes). A layer with no `exclude` key is unaffected — this is a purely
additive capability with no migration required for existing policies.

**Exclusions narrow legitimate scope.** Reach for `exclude` to express "this
is genuinely out of the rule's intended scope," not to silence a rule against
code that is actually in violation. Known debt that should eventually be
fixed belongs in an [exact violation baseline](../guides/migration-baselines.md),
not a layer exclusion — a baseline records what's wrong and tracks it toward
zero; an exclusion declares the code was never in scope to begin with.

An `exclude` entry that matches no namespace within its layer's included
scope — most often a typo, such as `Product.Modules.*.Persistnce` instead of
`Product.Modules.*.Persistence` — is reported as an `unmatched-layer-exclusion` policy-consistency finding
(governed by `analysis.policy_consistency`), so a silently inert exclusion is
visible instead of hiding a mistake.

## External layers

When a layer references namespaces whose assemblies may not be present in the scan environment, set `external: true`:

```yaml
layers:
  unity_engine:
    namespace: UnityEngine
    external: true
```

External layers suppress empty-layer configuration diagnostics, but they can still be used in dependency, allow-only, layer, cycle, independence, and protected-surface contracts.

For new vendor/framework leakage rules, prefer [`external_dependencies`](external-dependencies.md) and `strict_external` / `audit_external` contracts.

## Tips

- Prefer narrow concrete layers for important rules.
- Use glob layers for repeated sibling layouts where hand-listing every namespace would be brittle.
- Do not mix broad aggregate layers and overlapping child layers in the same ordered layer contract unless that overlap is deliberate and documented.
