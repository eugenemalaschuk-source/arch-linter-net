# External Dependencies

Use `external_dependencies` to model vendor, framework, or platform APIs that are not first-party architecture layers.

Typical examples:

- Unity runtime and editor namespaces;
- Entity Framework Core;
- database clients;
- cloud SDKs;
- payment SDKs;
- logging, telemetry, or messaging frameworks.

## YAML shape

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

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

## Matching rules

`namespace_prefixes` match exact namespaces and child namespaces.

`type_prefixes` match referenced full type names by prefix.

External dependency matching detects forbidden references through type-level metadata (base types, interfaces, fields, properties, method signatures, generic arguments) and method-body IL scanning (method calls, constructor calls, field/property access, type references inside method bodies). It does not analyze third-party package internals and should not be described as semantic data-flow analysis.

## Use with contracts

```yaml
contracts:
  strict_external:
    - id: domain-no-ef-core
      name: domain-must-not-reference-ef-core
      source: domain
      forbidden: [infrastructure_sdks]
      reason: Domain code must not expose infrastructure SDK types.
```

Use audit while discovering existing leakage:

```yaml
contracts:
  audit_external:
    - id: audit-application-sdk-leakage
      name: audit-application-sdk-leakage
      source: application
      forbidden: [infrastructure_sdks]
      reason: Discover SDK leakage before making this strict.
```

## External dependencies vs external layers

Prefer `external_dependencies` for vendor/framework leakage checks.

Use layer `external: true` only when you intentionally need layer-style semantics and want to suppress empty-layer diagnostics for namespaces that may not be present in the scan environment.

## Method-body detection

External dependency contracts now detect forbidden references inside method bodies via IL bytecode scanning. This covers method calls, constructor calls, field/property access, and type references. Violations include the source type, containing method name, forbidden group, and referenced external member.

For more granular call-pattern matching (e.g., specific method signatures), use [method-body contracts](../contracts/method-body.md).
