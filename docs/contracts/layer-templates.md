# Layer Template Contracts

Layer templates apply the same ordered layer shape to multiple namespace containers.

Groups:

- `strict_layer_templates`
- `audit_layer_templates`

## Example

```yaml
contracts:
  strict_layer_templates:
    - id: feature-clean-architecture
      name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
        - MyApp.Features.Inventory
        - MyApp.Features.Map
      layers:
        - name: Presentation
        - name: Application
          optional: true
        - name: Domain
      reason: Every feature follows the same internal dependency direction.
```

For container `MyApp.Features.Fishing`, this expands to:

```text
MyApp.Features.Fishing.Presentation
MyApp.Features.Fishing.Application
MyApp.Features.Fishing.Domain
```

The expanded layers follow the same outer-to-inner dependency direction as normal [layer contracts](layers.md).

## Optional layers

Optional layers do not produce a diagnostic when absent. If present, they must still follow the dependency direction.

```yaml
- name: Application
  optional: true
```

## Exhaustive container coverage

Use `exhaustive: true` when every immediate child namespace under the container must be mapped into a declared layer:

```yaml
contracts:
  strict_layer_templates:
    - id: feature-clean-architecture
      name: feature-clean-architecture
      containers:
        - MyApp.Features.Fishing
      layers:
        - name: Presentation
        - name: Application
        - name: Domain
      exhaustive: true
      reason: Every feature namespace must be mapped into the layer structure.
```

With exhaustive coverage, an unmapped namespace that contains loadable types becomes a violation. This helps catch new namespaces that silently bypass the intended architecture.
