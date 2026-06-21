# External Dependency Contracts

External dependency contracts prevent selected source layers from referencing forbidden vendor or framework dependency groups.

Groups:

- `strict_external`
- `audit_external`

## Define dependency groups

```yaml
external_dependencies:
  unity_editor:
    namespace_prefixes:
      - UnityEditor
    type_prefixes: []

  infrastructure_sdks:
    namespace_prefixes:
      - Microsoft.EntityFrameworkCore
      - Npgsql
    type_prefixes:
      - Stripe.StripeClient
```

## Add a contract

```yaml
contracts:
  strict_external:
    - id: domain-no-infrastructure-sdks
      name: domain-must-not-reference-infrastructure-sdks
      source: domain
      forbidden: [infrastructure_sdks]
      reason: Domain code must not expose infrastructure SDK types.
```

## What is checked

External dependency contracts detect forbidden references through:

- **Type-level metadata**: base types, interfaces, fields, properties, method signatures, generic arguments.
- **Method-body IL scanning**: method calls, constructor calls, field/property access, and type references inside method bodies.

They do not analyze third-party package internals and should not be presented as semantic data-flow analysis. This is static reference analysis, not runtime behavior validation.
