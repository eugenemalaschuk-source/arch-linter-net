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

1. **Generate** — create the baseline from current violations (`baseline generate`)
1. **Merge** — run `arch-linter-net --policy ... --baseline baseline.yml --mode strict` to enforce boundaries going forward
1. **Update** — run `baseline update` to add newly-introduced debt while preserving the `reason` text on entries that are still valid, without hand-editing YAML
1. **Prune** — run `baseline prune` to remove entries whose violation has been fixed or whose contract ID no longer exists, and see exactly what was removed
1. **Diff** — run `baseline diff` at any time to see new/existing/resolved/configuration-error entries without changing the file
1. **Verify** — run `baseline verify` in CI to fail the build if the baseline has drifted out of sync (resolved entries or unknown contract IDs), keeping the baseline honest over time

These five subcommands share `--config`/`--policy`, `--mode` (`strict`/`audit`/`all`),
`--condition-set`, and `--contract` (repeatable, restricts to specific contract IDs),
consistent with `validate`.

#### Update

```bash
arch-linter-net baseline update \
  --config architecture/dependencies.arch.yml \
  --baseline baseline.yml \
  --output baseline.yml \
  --reason "Newly accepted debt — tracked in #456"
```

Entries whose `(contract id, source_type, forbidden_reference)` still matches a
current violation are kept unchanged, including their original `reason`. New
violations are appended using the default or `--reason` text. Entries that no
longer match any violation are left in place — `update` never removes entries;
that is `prune`'s job.

#### Prune

```bash
arch-linter-net baseline prune \
  --config architecture/dependencies.arch.yml \
  --baseline baseline.yml \
  --output baseline.yml
```

Removes baseline entries that no longer match any current violation (resolved
debt) or that reference a contract ID that no longer exists in the policy
(configuration error), and reports exactly what was removed and why. Add
`--json` to get the removed-entry list as structured data.

#### Diff

```bash
arch-linter-net baseline diff \
  --config architecture/dependencies.arch.yml \
  --baseline baseline.yml
```

Read-only comparison of the baseline against current violations. Reports four
categories: **new** (unbaselined violations), **existing/frozen** (still
matched), **resolved** (stale entries), and **configuration errors** (unknown
contract IDs). Never writes a file.

#### Verify

```bash
arch-linter-net baseline verify \
  --config architecture/dependencies.arch.yml \
  --baseline baseline.yml
```

Runs the same comparison as `diff` but exits non-zero if any resolved entries
or configuration errors are found — intended as a CI gate that keeps a
baseline from silently accumulating stale debt. It does not fail on new,
unbaselined violations (that's `validate`'s job).

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

## Coverage baselines

`strict_coverage` and `audit_coverage` contracts (see
[architecture coverage](../contracts/coverage.md)) support the same
`ignored_violations` and baseline mechanism as ordinary dependency contracts.
This lets teams adopt coverage gates incrementally on a repository that
already has uncovered namespaces or stale rule-input references, rather than
having to resolve every coverage gap before turning the gate on.

```bash
arch-linter-net baseline generate \
  --config architecture/dependencies.arch.yml \
  --output baseline.yml \
  --reason "Coverage baseline — tracked in #103"
```

For a `namespace`-scoped coverage contract, each currently uncovered namespace
is captured as `source_type: <namespace>` /
`forbidden_reference: "uncovered namespace"`:

```yaml
version: 1
baseline:
  strict_coverage:
    - id: feature-namespace-coverage
      ignored_violations:
        - source_type: MyApp.Features.Legacy
          forbidden_reference: "uncovered namespace"
          reason: "Coverage baseline — tracked in #103"
```

For a `rule_input`-scoped coverage contract, each unresolved or empty-input
rule reference is captured as `source_type: <referenced-contract-id>` /
`forbidden_reference: <layer-name>`:

```yaml
version: 1
baseline:
  strict_coverage:
    - id: rule-input-coverage
      ignored_violations:
        - source_type: video-to-ghost-rule
          forbidden_reference: ghost
          reason: "Coverage baseline — tracked in #103"
```

`validate --baseline` suppresses these baselined coverage findings while still
reporting newly uncovered areas, exactly like ordinary dependency violations.
Coverage baseline entries only affect coverage contract findings — they never
suppress or otherwise interact with `strict`/`audit` dependency violations.
A coverage baseline entry whose underlying gap has since been resolved (the
namespace became covered, or the rule reference became resolved again) is
reported as a stale baseline entry through the same
`unmatched_ignored_violations` mechanism described above.
