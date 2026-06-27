# Contracts

ArchLinterNet contracts define executable architecture rules.

Most contract families have two variants:

- **strict** — blocking rules for the current architecture gate;
- **audit** — diagnostic rules for migration discovery and future-state visibility.

## Contract family map

| Family | Strict group | Audit group | Use when |
|--------|--------------|-------------|----------|
| [Dependency](dependency.md) | `strict` | `audit` | A source layer must not reference forbidden layers. |
| [Layer order](layers.md) | `strict_layers` | `audit_layers` | Dependencies must point inward through an ordered layer stack. |
| [Allow-only](allow-only.md) | `strict_allow_only` | `audit_allow_only` | A source layer may reference only explicitly allowed layers. |
| [Cycle](cycles.md) | `strict_cycles` | `audit_cycles` | Selected layers must not form directed dependency cycles. |
| [Acyclic sibling](acyclic-siblings.md) | `strict_acyclic_siblings` | `audit_acyclic_siblings` | Direct sibling namespaces under an ancestor must remain acyclic. |
| [Independence](independence.md) | `strict_independence` | `audit_independence` | A set of modules/layers must not reference each other. |
| [Protected surface](protected-surface.md) | `strict_protected` | `audit_protected` | A target layer may only be imported by approved layers. |
| [External dependency](external-dependencies.md) | `strict_external` | `audit_external` | Source code must not leak forbidden vendor/framework dependencies. |
| [Method body](method-body.md) | `strict_method_body` | `audit_method_body` | Source code must not call forbidden APIs inside executable bodies. |
| [Unity asmdef](asmdef.md) | `strict_asmdef` | `audit_asmdef` | Unity assembly definition references must follow architecture rules. |
| [Layer template](layer-templates.md) | `strict_layer_templates` | `audit_layer_templates` | The same ordered layer shape applies to multiple namespace containers. |
| [Namespace coverage](coverage.md) | `strict_coverage` | `audit_coverage` | First-party namespaces under configured roots must be modeled by layers, templates, or explicit exclusions. |

## Strict or audit?

Use strict contracts for rules that should be green on every pull request.

Use audit contracts when you are discovering existing coupling, preparing a future architecture, or migrating an existing codebase. Audit output should be visible, but it should not be confused with a passing architecture gate.

## Contract identity

Add an explicit `id` when a contract will be referenced by CI, baseline files, documentation, or issue discussions:

```yaml
contracts:
  strict:
    - id: domain-not-infrastructure
      name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Domain code must remain independent of infrastructure.
```

When `id` is omitted, ArchLinterNet derives one from `name`, but explicit IDs are more stable for long-lived policies.

## Unsupported rule types

Do not invent YAML fields or contract families. ArchLinterNet validates only the contract families documented in this section and in the YAML schema.

See [Supported capabilities and non-goals](../policy-format/supported-capabilities.md) before adding new policy concepts.
