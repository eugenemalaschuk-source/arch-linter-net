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
