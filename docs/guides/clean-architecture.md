# Clean Architecture Policy

This guide shows a typical inward-dependency policy.

## Layers

```yaml
layers:
  web:
    namespace: MyApp.Web
  infrastructure:
    namespace: MyApp.Infrastructure
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
```

## Target assemblies

```yaml
analysis:
  target_assemblies:
    - MyApp.Web
    - MyApp.Infrastructure
    - MyApp.Application
    - MyApp.Domain
```

## Inward layer rule

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

## Pure domain rule

```yaml
contracts:
  strict_allow_only:
    - id: domain-pure
      name: domain-allowed-dependencies
      source: domain
      allowed: []
      reason: Domain must not depend on other first-party layers.
```

## Infrastructure leakage rule

```yaml
external_dependencies:
  infrastructure_sdks:
    namespace_prefixes:
      - Microsoft.EntityFrameworkCore
      - Npgsql
    type_prefixes: []

contracts:
  strict_external:
    - id: domain-no-infrastructure-sdks
      name: domain-must-not-reference-infrastructure-sdks
      source: domain
      forbidden: [infrastructure_sdks]
      reason: Domain must not expose infrastructure SDK types.
```

## Migration variant

If existing code violates the target architecture, put future-state rules in audit first:

```yaml
contracts:
  audit:
    - id: audit-web-bypassing-application
      name: audit-web-bypassing-application
      source: web
      forbidden: [domain]
      reason: Discover web code that bypasses application use cases.
```

Promote audit rules to strict only when the team is ready for a blocking gate.
