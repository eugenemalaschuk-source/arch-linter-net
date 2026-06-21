# Allow-Only Contracts

Allow-only contracts use whitelist semantics: a source layer may reference only itself and explicitly allowed layers.

Groups:

- `strict_allow_only`
- `audit_allow_only`

## Example

```yaml
contracts:
  strict_allow_only:
    - id: domain-pure
      name: domain-allowed-dependencies
      source: domain
      allowed: []
      reason: Domain must not depend on other first-party layers.
```

## When to use

Use allow-only contracts when a layer should have a small known dependency surface:

- domain models;
- public contracts;
- shared abstractions;
- pure libraries;
- boundary packages.

## Allowed types

Use `allowed_types` for narrow exact type exceptions:

```yaml
contracts:
  strict_allow_only:
    - id: application-allowed-dependencies
      name: application-allowed-dependencies
      source: application
      allowed: [domain]
      allowed_types:
        - MyApp.Infrastructure.Abstractions.IClock
      reason: Application depends on Domain plus a temporary clock abstraction exception.
```

`allowed_types` entries are exact full type names. They are not namespace patterns.
