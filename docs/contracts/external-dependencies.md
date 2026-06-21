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

External dependency contracts match referenced type metadata visible from project types, such as:

- base types;
- interfaces;
- fields;
- properties;
- method signatures;
- generic arguments.

They do not analyze third-party package internals and should not be presented as semantic data-flow analysis.

## Current limitation

External dependency contracts are not a blanket guarantee for all implementation-body calls. For known forbidden API calls inside method bodies, use [method-body contracts](method-body.md) where appropriate.
