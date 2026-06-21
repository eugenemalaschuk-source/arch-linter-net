# Dependency Contracts

Dependency contracts check that a source layer does not reference forbidden layers.

Groups:

- `strict`
- `audit`

## Example

```yaml
contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code must depend on abstractions, not concrete infrastructure.
```

## Direct and transitive depth

By default, dependency contracts check direct type references.

Use `dependency_depth: transitive` when indirect paths should also fail:

```yaml
contracts:
  strict:
    - id: application-not-transitively-infrastructure
      name: application-must-not-transitively-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      dependency_depth: transitive
      reason: Application must not have any dependency path into Infrastructure.
```

Transitive checks are more expensive and should be used when the dependency path matters architecturally.

## Ignored violations

Use `ignored_violations` only for narrow frozen debt:

```yaml
ignored_violations:
  - source_type: MyApp.Application.Legacy.LegacyUseCase
    forbidden_reference: MyApp.Infrastructure.LegacyGateway
    reason: Existing debt tracked in #123.
```

For larger adoption work, prefer a generated [migration baseline](../guides/migration-baselines.md).
