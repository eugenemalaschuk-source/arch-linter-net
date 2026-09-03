# Architecture Health

`architecture-health/v1` is ArchLinterNet's canonical non-compensating summary of current architecture-governance evidence. It projects existing validation, applicability/completion, coverage, topology, metric, external-evidence, policy-inventory, baseline-debt, waiver-lifecycle, new-debt and weakening authorities; it does not invent a second scoring system.

Run it with:

```bash
arch-linter-net health \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode strict \
  --ensure-built \
  --execution-context local-review \
  --format json
```

## Gate and Health are different

The output keeps two decisions separate:

- `gate`: `pass`, `fail`, or `unassessable`;
- `health`: `healthy`, `debt`, `degrading`, `failing`, or `unassessable`.

A passing gate does not imply zero debt. A repository may be allowed to continue development while still carrying reviewed architecture debt.

| Gate | Health | Meaning |
| --- | --- | --- |
| `pass` | `healthy` | Required current evidence is assessable, blocking controls pass, and explicit waiver debt is zero. |
| `pass` | `debt` | Blocking controls pass but reviewed finding debt and/or valid explicit waiver debt remains. |
| `pass` or `fail` | `degrading` | Current evidence shows architectural regression such as new debt, weakening, a new/broadened waiver, or metric regression. The owning gate still determines whether that regression blocks the invocation. |
| `fail` | `failing` | A current blocking architecture requirement fails. |
| `unassessable` | `unassessable` | Required evidence cannot be trusted as complete or current. |

The CLI maps gate outcomes to exit codes: `pass -> 0`, `fail -> 1`, and `unassessable -> 2`. Exit `2` can therefore represent a valid Health document with `gate: unassessable`; distinguish that from an invalid invocation by reading the structured output.

## Non-compensating dimensions

Health dimensions do not offset one another. A healthy topology dimension cannot compensate for missing required external evidence; a strong metric cannot compensate for a current blocking dependency violation; a resolved finding cannot erase unrelated new debt.

This is why Architecture Health exposes no weighted score, letter grade or universal percentage. Counts and ratios are transparency evidence, not substitutes for the owning architecture semantics.

## Debt categories remain distinct

Architecture Health preserves the source of debt instead of collapsing everything into one count.

### Finding baseline debt

A migration baseline records reviewed existing normalized findings. It is a debt ledger for known occurrences and supports no-new-debt comparison. Removing a baseline entry only makes sense when its finding has actually been resolved or deliberately re-reviewed.

### Explicit waiver debt

A structured waiver is a policy-authored exception with an exact target fingerprint, stable waiver ID and lifecycle metadata. It is counted separately from finding baseline debt. A valid active waiver can coexist with `gate: pass`, but its presence prevents a zero-waiver `healthy` state.

### New debt and weakening

A new finding outside the reviewed baseline, a new or broadened waiver, or a policy edit that relaxes governance is change evidence. Health can therefore become `degrading` even when much of the current repository remains valid.

### Metric regression

Metric budgets consume canonical measurements. Absolute breaches and baseline-relative regressions are metric evidence, not finding- or waiver-debt counts. Incomplete metric scope is unassessable rather than an artificially low value.

## Waiver lifecycle effects

Structured waiver states are not equally harmless:

- `active` — complete structured metadata, still within its review window, and tied to the intended live occurrence;
- `stale` — the governed finding no longer matches the waiver target; the waiver should be removed;
- `expired` — the review window has elapsed;
- `metadata_incomplete` — legacy compatibility entry without complete structured lifecycle metadata;
- `invalid` — malformed or unsupported lifecycle metadata; this fails closed instead of suppressing a finding.

Under strict waiver lifecycle semantics, stale/expired/invalid evidence cannot be treated as ordinary harmless debt. See [Structured waivers](../policy-format/structured-waivers.md).

## Applicability and completeness

Health consumes applicability/completion evidence from the same analysis rather than guessing from a lack of findings. Required empty, missing, ambiguous or stale inputs can make the result `unassessable`.

Important examples:

- exhaustive topology with a new required unmapped or ambiguous first-party subject;
- a contract-surface exposure rule whose selected source universe cannot be evaluated completely;
- a metric whose required contributor scope is incomplete;
- a required external SARIF artifact that is absent, failed, malformed, or bound to the wrong repository/revision/scope;
- a missing required applicability record that prevents the denominator from being trusted.

A valid zero-result external SARIF artifact is different: if the trust proof succeeds and the selected run explicitly completed successfully with zero selected diagnostics, the evidence is evaluable.

## Policy inventory

The Health projection consumes the canonical `architecture-policy-inventory/v1` object when available. `effective_rule_count` counts effective authored controls once rather than counting findings, YAML lines, or source-set/runtime fan-out. Explicit ignore/waiver debt is projected from the same selected effective policy scope.

A missing inventory is missing evidence. Consumers must not turn it into `0 rules` or `0 ignores`.

## Badge projection

Generate the canonical Shields endpoint payload from a Health artifact:

```bash
arch-linter-net badge architecture-health \
  --input architecture-health.json \
  --output architecture-health-badge.json
```

The badge message combines canonical Health with explicit ignore debt and effective rule count, for example:

```text
architecture | HEALTHY · 0 ignores · 42 rules
architecture | DEBT · 3 ignores · 42 rules
```

The exact values and badge color are CLI-owned. CI may validate transport metadata and publish the finished JSON, but it must not reconstruct Health, recount waivers/rules, or invent zeroes. Missing or mismatched trusted promotion evidence must publish an explicit unassessable state rather than retain an older healthy badge as current.

The Architecture Health badge is distinct from the legacy `badge architecture-policy` projection and from GitHub Actions, SonarCloud or Codecov status badges.

## PR report projection

`report pr` consumes the canonical Health and architecture-change artifacts and renders reviewer Markdown without re-running analysis:

```bash
arch-linter-net report pr \
  --health architecture-health.json \
  --change architecture-change.json \
  --output architecture-pr-report.md
```

A publisher may carry that inert Markdown to a sticky pull-request comment only after validating its repository/PR/head/run/schema/size/hash transport evidence. The publisher must not compute report sections or remediation semantics itself.
