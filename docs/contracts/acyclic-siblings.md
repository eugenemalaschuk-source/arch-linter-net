# Acyclic Sibling Contracts

Acyclic sibling contracts automatically discover direct child namespaces under configured ancestor namespaces and ensure those siblings do not form cycles.

Groups:

- `strict_acyclic_siblings`
- `audit_acyclic_siblings`

## Example

```yaml
contracts:
  strict_acyclic_siblings:
    - id: feature-siblings-acyclic
      name: feature-siblings-acyclic
      ancestors:
        - MyApp.Features
        - MyApp.Modules
      reason: Feature sibling namespaces must not form dependency cycles.
```

## How grouping works

For ancestor `MyApp.Features`, these namespaces are grouped by their immediate child segment:

- `MyApp.Features.Audio.Player` -> `Audio`
- `MyApp.Features.Inventory.Domain` -> `Inventory`
- `MyApp.Features.Map.Presentation` -> `Map`

Dependencies between descendant types are attributed to the direct sibling groups.

## When to use

Use this contract when new sibling modules are added over time and manually listing every layer would be brittle.

Use normal [cycle contracts](cycles.md) when the layer list should be explicit.
