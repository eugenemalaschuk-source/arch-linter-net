# Architecture metrics

Metric declarations let you inspect stable architecture values before you set a
budget. They are optional, read-only policy data: `measure` reports their
values, while budget contracts participate in ordinary strict or audit
validation.

```yaml
topology:
  subject_kind: namespace
  mode: exhaustive
  scope:
    selectors:
      - namespace: MyApp
  nodes:
    - id: application
      mappings:
        - namespace: MyApp.Application
    - id: infrastructure
      mappings:
        - namespace: MyApp.Infrastructure

metrics:
  - id: application-outgoing
    kind: outgoing_component_count
    topology_node: application
  - id: application-footprint
    kind: component_footprint_count
    topology_node: application
    unit: project

contracts:
  strict_metric_budgets:
    - id: application-outgoing-limit
      metric: application-outgoing
      maximum: 3
  audit_metric_budgets:
    - id: application-minimum-footprint
      metric: application-footprint
      minimum: 2
```

The closed metric kinds are:

- `outgoing_component_count`
- `incoming_component_count`
- `external_dependency_group_count`
- `component_footprint_count` (requires `unit: project` or `unit: assembly`)
- `topology_type_count` (requires a `subject_kind: type` topology)
- `public_contract_surface_count` (requires `public_api_surface: <contract-id>`)

A `project` footprint uses the canonical project ownership resolved by the
analysis snapshot. Configure the policy's project/solution discovery inputs
when you need that metric; ArchLinterNet reports an unassessable scope rather
than treating an assembly name as a project identity.

## Absolute budgets

Budget contracts reference a declared metric by ID; they do not repeat its kind
or subject selector. Place an enforcing rule in `strict_metric_budgets` and an
informational rule in `audit_metric_budgets`. Each rule needs a unique `id` and
at least one non-negative integer bound:

- `minimum` fails only when the measured value is lower;
- `maximum` fails only when the measured value is higher;
- both are allowed when `minimum` is less than or equal to `maximum`.

The bounds are inclusive, so a value equal to a bound passes. A budget uses the
same value, effective scope, and canonically ordered contributors shown by
`measure`; it does not calculate another metric. A passing budget creates no
finding, while a violation identifies the value, breached limit, metric
subject, and contributors through the normal Human, JSON, SARIF, Testing, and
baseline paths.

`measure` remains the best first step for choosing a reviewed limit. If its
required metric scope is unassessable because of missing, unmapped, ambiguous,
stale, or unexpected-empty evidence, validation projects the same shared
applicability evidence for the budget instead of treating a partial low count
as a passing result.

## Baseline-relative budgets

A metric budget can ratchet growth from a reviewed scalar value instead of
declaring only an absolute bound. Relative budgets remain ordinary strict or
audit contracts: put a blocking budget in `strict_metric_budgets` and an
informational one in `audit_metric_budgets`. `baseline_mode` is the relative
dimension; strict/audit are still the enforcement modes. There is no third
`ratchet` mode.

The supported modes are:

- `no_worse_than_baseline` allows no increase over the reviewed value (allowed
  delta `0`);
- `max_delta` allows an increase up to the required non-negative integer
  `max_delta`.

The existing `maximum` can optionally remain as an absolute safety cap. The
effective threshold is the lower of the reviewed value plus the allowed delta
and that cap, when a cap is present. Relative budgets must not declare
`minimum`; absolute budgets without `baseline_mode` keep their existing
`minimum`/`maximum` behavior.

For example, these budgets share the metric definitions above:

```yaml
contracts:
  strict_metric_budgets:
    - id: application-outgoing-ratchet
      metric: application-outgoing
      baseline_mode: no_worse_than_baseline
  audit_metric_budgets:
    - id: application-footprint-ratchet
      metric: application-footprint
      baseline_mode: max_delta
      max_delta: 2
      maximum: 20
```

The reviewed values live in a version-3 baseline's separate top-level
`metric_baselines` collection. A scalar entry is identified only by its
canonical metric definition and subject fields, not by a budget ID, display
text, contributor label, or finding identity:

```yaml
version: 3
baseline:
  strict_metric_budgets: []
  audit_metric_budgets: []
metric_baselines:
  - metric_identity_version: 1
    metric_id: application-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
  - metric_identity_version: 1
    metric_id: application-footprint
    metric_kind: component_footprint_count
    native_subject: application
    unit: project
    effective_scope: application
    value: 2
```

`unit` is included when the metric definition declares one; omit it for an
unqualified metric. `native_subject` and `effective_scope` are the canonical
values reported by `measure`, so copy them from that output rather than
inventing display labels. The [migration baseline guide](../guides/migration-baselines.md#metric-baseline-capture)
describes how to capture and review these entries.

A relative budget is assessable only when its selected baseline contains one
matching scalar entry. A missing entry, unsupported identity version, or change
to the metric kind, native subject, unit, or effective scope is stale or
otherwise unassessable baseline evidence. It fails closed: the value is not
treated as zero, the budget does not pass, and the entry is never used as a
finding-level ignore. Ordinary validation does not refresh a reviewed value.

Run `baseline generate` explicitly after reviewing the current measurement to
capture a complete scalar value. `baseline update` and `baseline prune` are
finding-debt lifecycle commands; they preserve existing `metric_baselines`
values and never add, replace, or recalculate them. A new reviewed value
requires another explicit generation or a reviewed manual edit.

Run the report with:

```bash
arch-linter-net measure --policy architecture/dependencies.arch.yml
arch-linter-net measure --format json --max-contributors 10
```

Each value is a cardinality of canonical, ordinally ordered contributors.
Repeated references do not inflate a value; component relations are direct, not
transitive; self edges do not contribute. A zero is trustworthy only when the
required scope is complete. Unmapped, ambiguous, stale, missing, or otherwise
incomplete required evidence is reported through the shared applicability model
with no numeric value, and `measure` exits 2 rather than presenting a partial
low count as a clean result.
