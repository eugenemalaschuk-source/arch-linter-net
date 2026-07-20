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
entry grouped by contract ID. Newly generated baselines use format **version
2**, which carries a versioned, structured identity per entry instead of
relying on the `source_type`/`forbidden_reference` display text alone. Example
output:

```yaml
version: 2
baseline:
  strict:
    - id: app-boundaries
      ignored_violations:
        - source_type: MyApp.App.Legacy.LegacyService
          forbidden_reference: MyApp.Infrastructure.LegacyDb
          reason: "Initial baseline — migration tracked in #123"
          identity_version: 2
          contract_family: strict
          kind: dependency
          source_assembly: MyApp.App
          target_assembly: MyApp.Infrastructure
          target_member: MyApp.Infrastructure.LegacyDb
          occurrence: 0
        - source_type: MyApp.App.Old.OldController
          forbidden_reference: MyApp.Infrastructure.SqlRepo
          reason: "Initial baseline — migration tracked in #123"
          identity_version: 2
          contract_family: strict
          kind: dependency
          source_assembly: MyApp.App
          target_assembly: MyApp.Infrastructure
          target_member: MyApp.Infrastructure.SqlRepo
          occurrence: 0
```

`source_type`/`forbidden_reference` remain present as human-readable display
fields, but for version 2 entries they are **not** the identity — the
structured fields are. This is what makes two same-named types in different
assemblies distinguishable (`source_assembly`/`target_assembly`), and what
lets multiple distinct forbidden-call occurrences inside one source type each
get their own entry instead of collapsing into one (`occurrence`). Fields that
a particular contract family doesn't yet resolve (e.g. `source_member` for
most families) are simply omitted.

Baseline files written before this change use format **version 1** (the plain
`(source_type, forbidden_reference)` pair as identity). They continue to load
and match exactly as before — nothing about their behavior changes until you
explicitly run `baseline migrate` (below).

### Baseline lifecycle

1. **Generate** — create the baseline from current violations (`baseline generate`)
1. **Merge** — run `arch-linter-net --policy ... --baseline baseline.yml --mode strict` to enforce boundaries going forward
1. **Update** — run `baseline update` to add newly-introduced debt while preserving the `reason` text on entries that are still valid, without hand-editing YAML
1. **Prune** — run `baseline prune` to remove entries whose violation has been fixed or whose contract ID no longer exists, and see exactly what was removed
1. **Diff** — run `baseline diff` at any time to see new/matched/stale/configuration-error entries without changing the file
1. **Verify** — run `baseline verify` in CI to fail the build if the baseline has drifted out of sync (stale entries or unknown contract IDs), keeping the baseline honest over time
1. **Migrate** — run `baseline migrate` once, on demand, to deterministically upgrade an existing version 1 baseline to version 2's structured identity

These six subcommands share `--config`/`--policy`, `--mode` (`strict`/`audit`/`all`),
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

Runs the same comparison as `diff` but exits non-zero if any stale entries
or configuration errors are found — intended as a CI gate that keeps a
baseline from silently accumulating stale debt. It does not fail on new,
unbaselined violations (that's `validate`'s job).

#### Migrate

```bash
arch-linter-net baseline migrate \
  --config architecture/dependencies.arch.yml \
  --baseline baseline.yml \
  --output baseline.v2.yml \
  --dry-run
```

Deterministically upgrades a legacy **version 1** baseline to **version 2**'s
structured identity. Each legacy entry is correlated against freshly
collected current-codebase violations by its exact legacy
`(source_type, forbidden_reference)` pair, scoped to its contract ID:

- **Exactly one match** — the entry is rewritten using that violation's full
  structured identity; its `reason` is preserved verbatim.
- **Zero matches** — the entry no longer corresponds to any current
  violation. It is reported as `stale` and dropped from the migrated output
  (the underlying debt is gone; there's nothing to migrate).
- **More than one match** — the legacy pair is ambiguous: it could refer to
  more than one distinct violation now that identity is structured. Migration
  refuses to guess. The entry is reported as `ambiguous` and, outside of
  `--dry-run`, the whole run fails closed — **no file is written** until you
  resolve the ambiguity (typically by baselining the specific occurrences you
  intend to keep with a fresh `baseline generate --contract <id>` pass, or by
  accepting the new, disambiguated entries as new debt).

Run `--dry-run`/`--check` first to see the classification report without
writing anything — useful as its own CI gate (exit code 1 if any entries are
ambiguous, 0 otherwise). Once the report is clean, drop `--dry-run` and
provide `--output` to write the migrated file. `baseline migrate` never
writes to the same path as `--baseline` — pick a distinct `--output`, review
it, then swap it in for the original file yourself.

`--mode`/`--contract` scope which entries this run *attempts to classify*
(matched/stale/ambiguous), not which entries end up in the output. Entries
outside the requested scope are always carried through into the output
unchanged, reported as `out_of_scope` — a scoped `baseline migrate --mode
strict` never touches, reclassifies, or drops your audit debt, and `--contract
<id>` never touches other contracts' debt. Classification itself always runs
against the full current violation set regardless of `--mode`/`--contract`,
so an out-of-scope entry is never misclassified as `stale` just because this
run didn't ask about it.

Migration is opt-in and on-demand: nothing about `validate`, `generate`,
`update`, `prune`, `diff`, or `verify` changes for a baseline you haven't
migrated. Version 1 files keep working with their existing behavior
indefinitely.

### Merge semantics

When `--baseline <path>` is provided, the baseline entries are merged into the
policy's `ignored_violations` lists before validation. The merge:

- Appends new entries to each contract's existing ignores
- Reports an error if a baseline entry references a contract ID that doesn't
  exist in the policy (exit code 2)
- Deduplicates and matches using the **same identity notion baseline
  comparison uses** — version 1 entries by the exact `(source_type,
  forbidden_reference)` pair, version 2 entries by the full structured
  `ArchitectureViolationIdentity` (contract family, kind, source/target
  assembly, source/target type and member, and an occurrence discriminator).
  A version-2 entry's assembly/member/occurrence fields are exactly what
  `validate --baseline` uses to decide whether a given violation is
  suppressed — this is what makes `validate`, `diff`, `verify`, and `migrate`
  agree on identity, not just the read-only comparison commands.

One baseline entry always suppresses exactly one identity — two same-named
types in different assemblies, or two distinct forbidden calls in the same
source type, are never treated as the same entry under version 2, in
`validate` just as much as in `diff`/`verify`.

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
version: 2
baseline:
  strict_coverage:
    - id: feature-namespace-coverage
      ignored_violations:
        - source_type: MyApp.Features.Legacy
          forbidden_reference: "uncovered namespace"
          reason: "Coverage baseline — tracked in #103"
          identity_version: 2
          contract_family: coverage
          kind: coverage
          target_member: "uncovered namespace"
          occurrence: 0
```

For a `rule_input`-scoped coverage contract, each unresolved or empty-input
rule reference is captured as `source_type: <referenced-contract-id>` /
`forbidden_reference: <layer-name>`:

```yaml
version: 2
baseline:
  strict_coverage:
    - id: rule-input-coverage
      ignored_violations:
        - source_type: video-to-ghost-rule
          forbidden_reference: ghost
          reason: "Coverage baseline — tracked in #103"
          identity_version: 2
          contract_family: coverage
          kind: coverage
          target_member: ghost
          occurrence: 0
```

Coverage identity does not yet carry assembly/member qualification (it's
categorical by nature — a namespace or a rule reference, not a symbol) — the
`occurrence` discriminator alone is enough to keep distinct coverage findings
from colliding.

`validate --baseline` suppresses these baselined coverage findings while still
reporting newly uncovered areas, exactly like ordinary dependency violations.
Coverage baseline entries only affect coverage contract findings — they never
suppress or otherwise interact with `strict`/`audit` dependency violations.
A coverage baseline entry whose underlying gap has since been resolved (the
namespace became covered, or the rule reference became resolved again) is
reported as a stale baseline entry through the same
`unmatched_ignored_violations` mechanism described above.
