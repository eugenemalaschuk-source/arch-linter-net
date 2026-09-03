# Architecture Health

`architecture-health/v1` is ArchLinterNet's canonical non-compensating summary of current architecture-governance evidence. It projects existing validation, applicability/completion, coverage, topology, metric, external-evidence, policy-inventory, baseline-debt, waiver-lifecycle, new-debt and weakening authorities; it does not invent a second scoring system.

Run it with:

```bash
arch-linter-net health \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --base-context artifacts/policy-base.json \
  --current-context artifacts/policy-current.json \
  --mode strict \
  --ensure-built \
  --execution-context local-review \
  --format json
```

`--base-context` and `--current-context` are optional as a pair. Supply both when Health must include policy-weakening evidence. When the policy declares required external evidence, also pass the same `--external-evidence` and `--evidence-*` bindings used for current validation; evidence held by another CLI process is not reused automatically.

## Gate and Health are different

The output keeps two decisions separate:

- `gate`: `pass`, `fail`, or `unassessable`;
- `health`: `healthy`, `debt`, `degrading`, `failing`, or `unassessable`.

A passing gate does not imply zero debt. A repository may be allowed to continue development while still carrying reviewed architecture debt.

| Gate | Health | Meaning |
| --- | --- | --- |
| `pass` | `healthy` | All required evidence is assessable, configured current authorities pass, and no reviewed finding debt, explicit waiver debt, new debt, weakening, or metric regression exists. |
| `pass` | `debt` | Blocking controls pass but reviewed finding debt and/or valid explicit waiver debt remains. |
| `pass` or `fail` | `degrading` | Current evidence shows architectural regression such as new debt, weakening, a new/broadened waiver, reportable warning coverage, audit-only diagnostics, or metric regression. The owning authority still determines whether that regression blocks the gate. |
| `fail` | `failing` | A current blocking architecture requirement fails. |
| `unassessable` | `unassessable` | Required evidence cannot be trusted as complete or current. |

The deterministic non-compensating precedence is:

```text
unassessable > failing > degrading > debt > healthy
```

For example, an unassessable required evidence dimension dominates an otherwise current strict failure, while both dimensions remain available for drill-down.

The CLI maps gate outcomes to exit codes: `pass -> 0`, `fail -> 1`, and `unassessable -> 2`. Exit `1` or `2` can accompany a valid `architecture-health/v1` document; distinguish that from an invalid invocation by reading the structured output.

## Canonical dimensions

The JSON and human projections retain one deterministic ordered list of separately authoritative dimensions:

1. `current_evaluation`;
1. `applicability`;
1. `audit_evidence`;
1. `coverage`;
1. `topology`;
1. `metrics`;
1. `external_evidence`;
1. `policy_inventory`;
1. `reviewed_finding_debt`;
1. `new_architecture_debt`;
1. `waiver_debt`;
1. `policy_weakening`;
1. `history`.

A dimension can be `pass`, `debt`, `degrading`, `fail`, `unassessable`, `not_applicable`, or `not_configured` according to its owning authority. `history` is currently advisory/not configured in the canonical projection; it does not silently become a release gate.

Dimensions do not offset one another. A healthy topology dimension cannot compensate for missing required external evidence; a strong metric cannot compensate for a current blocking dependency violation; a resolved finding cannot erase unrelated new debt.

This is why Architecture Health exposes no weighted score, letter grade or universal percentage. Counts and ratios are transparency evidence, not substitutes for the owning architecture semantics.

## Debt categories remain distinct

Architecture Health preserves the source of debt instead of collapsing everything into one count.

### Finding baseline debt

A migration baseline records reviewed existing normalized findings. It is a debt ledger for known occurrences and supports no-new-debt comparison. Removing a baseline entry only makes sense when its finding has actually been resolved or deliberately re-reviewed.

### Explicit waiver debt

A structured waiver is a policy-authored exception with an exact target fingerprint, stable waiver ID and lifecycle metadata. It is counted separately from finding baseline debt. A valid active waiver can coexist with `gate: pass`, but its presence prevents `health: healthy`.

### New debt and weakening

A new finding outside the reviewed baseline, a new or broadened waiver, or a policy edit that relaxes governance is change evidence. Health can therefore become `degrading` even when much of the current repository remains valid. When the owning weakening/debt gate is configured as blocking, the independent gate remains `fail`.

### Metric regression

Metric budgets consume canonical measurements. Absolute breaches and baseline-relative regressions are metric evidence, not finding- or waiver-debt counts. Incomplete metric scope is unassessable rather than an artificially low value.

## Waiver lifecycle effects

Structured waiver states are not equally harmless:

- `active` — complete structured metadata, still within its review window, and tied to the intended live occurrence;
- `stale` — the governed finding no longer matches the waiver target; the waiver should be removed;
- `expired` — the review window has elapsed;
- `metadata_incomplete` — legacy compatibility entry without complete structured lifecycle metadata;
- `invalid` — malformed or unsupported lifecycle metadata; this fails closed instead of suppressing a finding.

When more than one predicate could apply, canonical lifecycle classification uses this precedence:

```text
invalid > expired > stale > active > metadata_incomplete
```

Under strict waiver-lifecycle semantics, stale, expired, metadata-incomplete, or invalid evidence follows the selected profile's blocking state and cannot be flattened into ordinary reviewed debt. See [Structured waivers](../policy-format/structured-waivers.md).

## Applicability and completeness

Health consumes applicability/completion evidence from the same analysis rather than guessing from a lack of findings. Required empty, missing, ambiguous or stale inputs can make the result `unassessable`.

Important examples:

- exhaustive topology with a new required unmapped or ambiguous first-party subject;
- a contract-surface exposure rule whose selected source universe cannot be evaluated completely;
- a metric whose required contributor scope is incomplete;
- a required external SARIF artifact that is absent, failed, malformed, or bound to the wrong repository/revision/scope;
- a missing required applicability record that prevents the denominator from being trusted.

A valid zero-result external SARIF artifact is different: if the trust proof succeeds and the selected run explicitly completed successfully with zero selected diagnostics, the evidence is evaluable.

## Policy inventory and applicability are not the same count

The Health projection consumes the canonical `architecture-policy-inventory/v1` object when available. `effective_rule_count` counts every effective authored control once after imports and conditions, rather than counting findings, YAML lines, or source-set/runtime fan-out. Explicit ignore/waiver debt is projected from the same selected effective policy scope.

The applicability/evaluability denominator contains only controls that require applicability proof. It may therefore be smaller than `effective_rule_count`. A missing required applicability record cannot shrink that denominator or be represented as complete. Neither count is a quality score.

A missing policy inventory is missing evidence. Consumers must not turn it into `0 rules` or `0 ignores`.

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

The exact values and badge color are CLI-owned. CI may validate transport metadata and publish the finished JSON, but it must not reconstruct Health, recount waivers/rules, or invent zeroes. Missing or mismatched trusted promotion evidence must publish `UNASSESSABLE · ? ignores · ? rules` rather than retain an older healthy badge as current.

The Architecture Health badge is distinct from the legacy `badge architecture-policy` projection and from GitHub Actions, SonarCloud or Codecov status badges.

## PR report projection

`report pr` consumes canonical Health and architecture-change JSON and renders reviewer Markdown without re-running analysis:

```bash
arch-linter-net report pr \
  --health architecture-health.json \
  --change architecture-change.json \
  --output architecture-pr-report.md
```

The Health reporting evidence and change report must carry the same non-empty execution context and selected mode. Because a failing or unassessable Health gate exits `1` or `2` while still producing a valid document, a CI report producer should retain and schema-check the JSON before a separate required gate blocks the pull request. It must not convert the underlying architecture decision into a pass.

A publisher may carry the inert Markdown to one sticky pull-request comment only after validating its repository/PR/head/run/schema/size/hash transport evidence. The publisher must not execute PR content, compute report sections, or infer Architecture Health from arbitrary workflow status.
