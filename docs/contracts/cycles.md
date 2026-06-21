# Cycle Contracts

Cycle contracts detect directed dependency cycles between selected layers.

Groups:

- `strict_cycles`
- `audit_cycles`

## Example

```yaml
contracts:
  strict_cycles:
    - id: main-layer-cycles
      name: main-layer-cycles
      layers:
        - web
        - application
        - domain
        - infrastructure
      reason: Main architecture layers must not form dependency cycles.
```

## When to use

Use cycle contracts when cross-layer references may exist but circular coupling must not.

For modules that must not reference each other at all, use [independence contracts](independence.md). For sibling namespaces that should be discovered automatically, use [acyclic sibling contracts](acyclic-siblings.md).

## Diagnostics

Cycle diagnostics describe the layer path, for example:

```text
application -> infrastructure -> application
```

Each configured cycle contract evaluates only the layers listed in that contract.
