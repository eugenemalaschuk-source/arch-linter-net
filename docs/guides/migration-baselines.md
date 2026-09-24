# Migration Baselines

A finding baseline records **reviewed existing violations** so a repository can
reject new debt without fixing all old debt at once. It is not a list that CI
regenerates whenever a rule fails.

## Strict vs Audit

Strict and audit collections select different sets of contracts. Use strict
for an enforcing boundary and audit for discovery, but configure CI explicitly:
a standalone audit command can return 1. An advisory step must preserve its
report without making that result a required merge check. A combined
`--mode strict,audit` command fails if either selected mode fails. See
[exit codes](../usage/exit-codes.md).

## Ignored violations (frozen debt)

Choose the mechanism before writing an exception:

| Mechanism | Meaning |
| --- | --- |
| Finding baseline | Specific existing findings accepted for migration. |
| Structured waiver | An explicit policy exception with exact target, owner and expiry. |
| Scope exclusion | A deliberate subject outside one governed universe, with a reason. |

Manual `ignored_violations` and generated baseline entries are not the same
authoring workflow. Matcher-only manual ignores are legacy compatibility input.
In a new v2 policy, use [structured waivers](../policy-format/structured-waivers.md)
for a temporary exception, not an old three-field matcher copied from a baseline.
Policy v1 preserves compatibility defaults; migration to v2 is deliberate.
Do not turn a generated baseline entry into a manual waiver by copying its YAML.

## Automated Baseline Generation

### Generate a baseline

Prepare the selected build state, inspect the findings, then capture the intended
debt. The command is an explicit local review operation:

```bash
arch-linter-net baseline generate --config architecture/arch.yml \
  --output architecture/baseline.arch.yml --reason "Reviewed migration debt"
```

Review the proposed entries and commit them with the policy. Validate with that
baseline explicitly:

```bash
arch-linter-net --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --mode strict --ensure-built
```

Generation writes version 2 finding identities when no selected relative metric
budget requires scalar capture. With selected relative budgets, it writes
version 3, preserving finding entries under `baseline` and scalar values under
`metric_baselines`. These are baseline document versions, not policy or package
versions.

Version 1 finding entries use the legacy source/reference pair. Version 2 uses
structured identity: family/kind, source and target assembly/type/member, and an
occurrence discriminator where provided. Display text is not the structured
identity. Let the command generate these fields instead of guessing them.

### Metric baseline capture

Relative budgets use `baseline_mode: no_worse_than_baseline` or `max_delta`.
The threshold is the reviewed value plus the allowed increase, limited by an
optional absolute `maximum`. `minimum` is not supported for a relative budget.
See [architecture metrics](../policy-format/architecture-metrics.md#baseline-relative-budgets).

Use explicit reviewed generation to capture a complete measurement. A scalar
entry identifies the metric definition and subject, not the budget rule ID:

```yaml
version: 3
baseline: {}
metric_baselines:
  - metric_identity_version: 1
    metric_id: application-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
```

Copy `native_subject` and `effective_scope` from actual measurement output; the
values above are illustrative. Include `unit` when the metric declares one.
Missing, stale, ambiguous or incompatible scalar identity is unassessable, not
zero. `baseline update` and `prune` preserve existing scalar values and do not
recalculate them. A new scalar starting point needs generation or a reviewed
manual edit.

### Baseline lifecycle

| Command | Writes | Main use |
| --- | --- | --- |
| `generate` | Explicit output | Capture current findings and selected complete scalar measurements. |
| `diff` | No | Review current versus accepted debt. |
| `verify` | No | Fail on invalid/drifted baseline evidence. |
| `update` | Explicit output | Add newly reviewed finding debt; retain existing entries. |
| `prune` | Explicit output | Remove obsolete finding entries; retain ambiguous entries. |
| `migrate` | A distinct output | Upgrade supported legacy v1 finding identities to v2. |

`generate`, `update`, `prune`, `diff` and `verify` share policy, mode,
condition-set and contract selection. `migrate` examines the entire baseline:
it does not accept `--mode` or `--contract`.

### Reviewing a change before it happens

Without `--output`, a writing command presents its proposed document on stdout.
`--dry-run` with an output path previews without writing. `--json` includes the
proposal as `proposedContent` with classified entries and counts.

`generate` and `migrate` require `--force` to replace an existing destination.
`update`/`prune` may write back to the same baseline path; another existing path
requires `--force`. Path spelling/case matters. Writes are atomic. A no-op prune
preserves the original document bytes rather than reformatting it.

### Entry lifecycle

| Status | Meaning |
| --- | --- |
| `new` | A current finding has no exact accepted entry. |
| `matched` | Accepted and current canonical identities agree. |
| `resolved` | A valid, evaluable entry no longer has a live finding. |
| `stale` | The contract, source or identity can no longer be validly evaluated. |
| `changed` | A predecessor/successor is identifiable but needs review because identity changed. |
| `ambiguous` | More than one correspondence is possible. |
| `configuration-error` | Invalid input prevents a safe comparison. |

Use the structured `suppresses` field; do not infer suppression from similar
messages. Only an accepted match suppresses a finding. `changed`, `stale`,
`ambiguous` and invalid evidence are not automatic approval.

Disposition is separate: `reported`, `added`, `retained` or `removed`. A resolved
entry can be retained by update and removed by prune without changing its status.
JSON carries all seven status counts, entries and canonical identities.

### Reviewing a requalified identity

When a contract gains an identity dimension, inspect `diff`/`verify`. Review the
new exact occurrence, add it deliberately, then prune obsolete entries. Do not
broaden an old identity or call v1-to-v2 migration on an already-v2 document.
Metadata is carried over only where correspondence is deterministic.

### SARIF and Testing API comparison results

`baseline diff`, `verify` and `migrate` accept `--format sarif`, carrying
`baseline_status` and identity properties. Testing consumers can use
`WithBaseline(path)` with `DiffBaseline()`, `VerifyBaseline()` or
`MigrateBaseline()`; writing actions still belong in an explicit review workflow.

### Comments and issue metadata

Update/prune preserve leading comments and retained entries' `reason`/`issue`.
Inline comments cannot be safely reattached after restructuring, so rewriting
such a file is refused; use `--dry-run` and merge the proposed change manually.
A trailing unquoted `# comment` also counts as a comment. `#` inside a quoted
value does not.

### Per-contract and per-family reasons

For a new entry, reason precedence is `--reason-for-contract id=text`, then
`--reason-for-family family=text`, then `--reason`, then the default. Retained
entries keep their reason. Malformed mappings and duplicate keys are rejected.
The family key omits the strict/audit prefix, such as `package_dependency`.

#### Update

```bash
arch-linter-net baseline update --config architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --output architecture/baseline.arch.yml \
  --reason "New debt accepted after review" --dry-run
```

Review before removing `--dry-run`. Update retains resolved and ambiguous entries;
it does not perform pruning or refresh scalar metric values.

#### Prune

```bash
arch-linter-net baseline prune --config architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --output architecture/baseline.arch.yml --dry-run
```

Inspect each proposed removal. Obsolete finding entries can be removed; ambiguous
live debt is retained rather than guessed away.

#### Diff

```bash
arch-linter-net baseline diff --config architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --json
```

Diff is a report, not a no-new-debt gate. A completed comparison can succeed while
listing differences; malformed inputs/runtime failures still fail. Do not describe
it as a command that always exits 0 regardless of errors.

#### Verify

```bash
arch-linter-net baseline verify --config architecture/arch.yml \
  --baseline architecture/baseline.arch.yml
```

Verify checks baseline integrity/current applicability. It is not a substitute
for rejecting new unbaselined findings through validation or `gate`. Both verify
and diff can be read-only CI steps; avoid duplicating a comparison already done
by your selected gate.

#### Migrate

```bash
arch-linter-net baseline migrate --config architecture/arch.yml \
  --baseline architecture/baseline-v1.yml --output architecture/baseline-v2.yml --dry-run
```

Migration correlates every legacy entry with current findings in its own
contract. A unique match gets structured identity. Ambiguous matches prevent
writing; inspect the classification before removing `--dry-run`. A no-longer-live
entry is not carried forward as newly accepted debt. Use a distinct output path,
review the result, then replace the old baseline deliberately.

### Merge semantics

Validation merges the selected baseline using its identity format. It rejects
unknown contract IDs instead of silently ignoring those entries. Two same-named
types in different assemblies or distinct calls are not one structured finding.
Legacy v1's weaker matcher identity needs special care when migrating ambiguity.

### Stale baseline entries

Resolved findings and invalid baseline references are different review cases.
Unmatched-ignore/governance diagnostics can still block the selected workflow.
Do not rename them all "new debt" or automatically accept replacements.

## Coverage baselines

Coverage findings participate in the same reviewed finding-debt workflow. Generate
entries from actual coverage diagnostics; do not fabricate identity fields from
a namespace label. A coverage entry does not suppress an ordinary dependency
finding. Once a gap is fixed, review and remove its obsolete baseline entry.
See [coverage contracts](../contracts/coverage.md).

## Gate new debt without rewriting the baseline

```bash
arch-linter-net gate --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --mode all --ensure-built --format json
```

Gate rejects new debt and invalid/drifted persistent-debt evidence. Paired
base/current policy contexts add the separate weakening guardrail. An error in
that guardrail can block even with no new finding; warnings do not become fake
baseline entries. See the [complete review workflow](single-tool-workflow.md).
