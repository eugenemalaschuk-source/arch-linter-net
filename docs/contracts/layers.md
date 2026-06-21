# Layer Contracts

Layer contracts enforce dependency direction through an ordered list of layers.

Groups:

- `strict_layers`
- `audit_layers`

## Ordering model

Layers are listed from outermost to innermost. A layer may depend on layers below it, but not on layers above it.

```yaml
contracts:
  strict_layers:
    - id: clean-architecture-layering
      name: clean-architecture-layering
      layers:
        - web
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward the domain.
```

In this example, `web` may reference `application` or `domain`, but `domain` must not reference `application`, `infrastructure`, or `web`.

## Good uses

Use layer contracts for:

- Clean Architecture style inward dependencies;
- Onion Architecture rings;
- UI -> Application -> Domain ordering;
- package/module internal layering.

## Avoid overlap confusion

Do not mix a broad aggregate layer and a child layer in the same ordered contract unless that overlap is intentional. Overlapping layer definitions can make diagnostics hard to interpret.

For repeated internal layer shapes, use [layer templates](layer-templates.md).
