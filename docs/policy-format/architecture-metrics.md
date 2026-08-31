# Architecture metrics

Metric declarations let you inspect stable architecture values before you set a
budget. They are optional, read-only policy data: `measure` reports their
values, while threshold and baseline enforcement are separate workflows.

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
