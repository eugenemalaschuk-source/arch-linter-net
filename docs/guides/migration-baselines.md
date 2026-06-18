# Migration Baselines

ArchLinterNet supports a **frozen-debt** workflow for repositories that already have
architecture violations and want to enforce boundaries going forward without fixing
everything at once.

## Strict vs Audit

- **Strict contracts** block the build on violation. Use for boundaries you want
  enforced immediately.
- **Audit contracts** report violations without blocking. Use for visibility during
  a migration.

Both contract types go in the same YAML file under separate sections:

```yaml
contracts:
  strict:        # blocks on violation
    - name: app-must-not-depend-on-infrastructure
      source: app
      forbidden: [infrastructure]
      reason: This boundary is enforced now.

  audit:         # reports only, doesn't block
    - name: domain-must-not-depend-on-infrastructure
      source: domain
      forbidden: [infrastructure]
      reason: Tracking for migration — will become strict in Q2.
```

## Ignored violations (frozen debt)

The `ignored_violations` section allows you to acknowledge existing violations
so they don't cause failures, while still preventing new ones:

```yaml
contracts:
  strict:
    - name: app-boundaries
      source: app
      forbidden: [infrastructure]
      ignored_violations:
        - source_type: MyApp.App.Legacy.LegacyService
          forbidden_reference: MyApp.Infrastructure.LegacyDb
          reason: "Known debt — tracked in #1234"
```

When a violation matches an ignored entry, it is suppressed. Any violation
that does **not** match an ignored entry will still fail the build.

This approach enables:

- Freezing existing violations without fixing them immediately
- Tracking debt with issue references
- Gradually removing ignored entries as violations are resolved
- Preventing regression (new violations are still caught)
