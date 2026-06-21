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

## Automated Baseline Generation

ArchLinterNet can automatically generate a baseline file from the current state
of violations. This is useful when adopting architecture rules for the first time
on an existing codebase.

### Generate a baseline

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output baseline.yml \
  --reason "Initial baseline — migration tracked in #123"
```

The generated file captures every current violation as an `ignored_violations`
entry grouped by contract ID. Example output:

```yaml
version: 1
baseline:
  strict:
    - id: app-boundaries
      ignored_violations:
        - source_type: MyApp.App.Legacy.LegacyService
          forbidden_reference: MyApp.Infrastructure.LegacyDb
          reason: "Initial baseline — migration tracked in #123"
        - source_type: MyApp.App.Old.OldController
          forbidden_reference: MyApp.Infrastructure.SqlRepo
          reason: "Initial baseline — migration tracked in #123"
```

### Baseline lifecycle

1. **Generate** — create the baseline from current violations
2. **Merge** — run `arch-linter-net --policy ... --baseline baseline.yml --mode strict` to enforce boundaries going forward
3. **Clean up** — as violations are fixed, remove individual entries from the baseline file
4. **Regenerate** — when the codebase changes significantly, regenerate to capture the new state

### Merge semantics

When `--baseline <path>` is provided, the baseline entries are merged into the
policy's `ignored_violations` lists before validation. The merge:

- Appends new entries to each contract's existing ignores
- Skips duplicate `(source_type, forbidden_reference)` pairs that already exist
- Reports an error if a baseline entry references a contract ID that doesn't
  exist in the policy (exit code 2)

### Stale baseline entries

Baseline entries that no longer match any current violation are detected by the
runner's unmatched ignored violation tracking (same as manual ignores). When
`analysis.unmatched_ignored_violations` is set to `error` (default), stale
baseline entries produce a blocking failure, encouraging proactive cleanup.
