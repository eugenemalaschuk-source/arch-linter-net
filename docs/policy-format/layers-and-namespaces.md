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

## Reserved selector predicates

The reviewed CEL policy model for issue #162 reserves a future explicit
`layers.<name>.selector.when` field for narrow boolean predicates over the
matched candidate type. That field is **not implemented yet** and must remain
fail-closed until issue #163 lands.

Important boundary:

- ordinary selector fields such as `role`, `metadata`, `namespace`, and
  `namespace_suffix` are always literal values, never implicitly parsed as
  expressions;
- if CEL-backed predicates are introduced later, they will use only an explicit
  `when` field;
- policy authors and AI agents should not invent `when` in current runtime
  policies.

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
