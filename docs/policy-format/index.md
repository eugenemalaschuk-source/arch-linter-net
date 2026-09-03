# Policy Format

An ArchLinterNet policy is repository-owned YAML that declares the architecture facts and contracts a validation run should enforce.

The packaged JSON Schema is the syntax authority. Runtime validators and the executable contract-family registry are the behavior authority. `archlinternet.capabilities.json` is the machine-readable public capability inventory. `make lint-docs` checks the public reference against those sources.

## Root policy

A selected root policy uses `version: 1` or `version: 2` and normally contains:

```yaml
version: 2
name: My Architecture Contract

imports: []

layers: {}

external_dependencies: {}
packages: {}
framework_references: {}
source_sets: {}

classification: {}

topology: {}

metrics: []
external_evidence: []

analysis: {}

contracts: {}
```

`version: 1` preserves compatibility waiver defaults for existing policies. `version: 2` defaults to strict structured-waiver lifecycle governance and is the recommended starting point for new policy authoring. The policy version is a persisted policy-contract version, not the NuGet package version.

`imports`, external/package/framework groups, source sets, classification, topology, metrics, and external evidence are optional. The root schema requires the root identity and the core `layers`, `analysis`, and `contracts` containers; imported fragments can contribute entries during composition.

See [YAML schema reference](../reference/yaml-schema.md), [Policy imports](imports.md), [Structured waivers](structured-waivers.md), and [Extended governance adoption](../guides/extended-governance-adoption.md).

## Declared topology

An optional native `topology` section declares stable components, their mappings, allowed directional edges, and the bounded observed subject universe that validation assesses. It is not a diagram language and does not infer unreviewed components. See [Declared topology](declared-topology.md) for the complete mapping, completeness, and reviewed out-of-scope semantics.

## Layers: namespaces and semantic selectors

A layer can be namespace-backed, selector-backed, or both.

```yaml
layers:
  domain:
    namespace: MyApp.Domain

  commands:
    selector:
      role: command
      metadata:
        bounded_context: Sales

  sales_commands:
    namespace: MyApp.Sales
    selector:
      role: command
```

Selector-only layers are supported. When both `namespace` and `selector` are present, a type must satisfy both. `namespace_suffix`, `exclude`, `overlaps_with`, and CEL-backed selector `when` predicates have the constraints documented in [Layers and namespace patterns](layers-and-namespaces.md) and [Semantic classification](semantic-classification.md).

## Analysis inputs

Policies can analyze explicit target assemblies, discovered projects, or a solution:

```yaml
analysis:
  solution: MyApp.sln
  project_exclude:
    - "**/*.Tests/**"
  configuration: Debug
  coverage: error
  policy_weakening: error
  waiver_lifecycle_profile: strict
```

Other analysis controls include assembly search paths, source roots, target framework/build selectors, condition sets, ignored-violation behavior, policy-consistency severity, coverage severity, policy-weakening severity, and waiver lifecycle profile.

Normal validation does not silently build. Use `--ensure-built` when the CLI should build the selected project graph and verify its build-state receipt before validation.

## Contract modes

Each contract family has strict and audit groups.

- **Strict** contracts are blocking architecture requirements.
- **Audit** contracts report migration/future-state findings without turning every discovered rule into an immediate blocking policy.

The complete current inventory is in [Contract families](../contracts/index.md).

## Architecture coverage

Coverage is a normal contract family (`strict_coverage` / `audit_coverage`) with six implemented scopes:

<!-- coverage-scope: namespace -->

- `namespace`

<!-- coverage-scope: project -->

- `project`

<!-- coverage-scope: assembly -->

- `assembly`

<!-- coverage-scope: dependency_edge -->

- `dependency_edge`

<!-- coverage-scope: rule_input -->

- `rule_input`

<!-- coverage-scope: semantic_role -->

- `semantic_role`

Use coverage to make policy omissions visible: unmapped first-party namespaces/projects/assemblies, ungoverned observed dependency edges, stale or unresolved rule inputs, and semantic roles not covered by selector-backed/contextual governance.

See [Coverage contracts](../contracts/coverage.md).

## Extended governance

The complete static governance cycle composes existing authorities rather than introducing a second architecture engine:

```text
policy check
  -> architecture validation + applicability/completeness
  -> declared topology + visible contract surfaces
  -> finding baseline debt + structured waiver lifecycle + weakening
  -> measurements and budgets
  -> current-context external SARIF evidence
  -> architecture change
  -> Architecture Health
  -> PR Markdown / JSON / SARIF / Health badge
```

Use [Architecture metrics](architecture-metrics.md), [External evidence](external-evidence.md), [Architecture Health](../reference/architecture-health.md), and the [complete single-tool workflow](../guides/single-tool-workflow.md) for the delivered end-to-end path.

## Semantic classification

Implemented classification inputs include attribute, assembly-attribute, inheritance, and namespace facts. Selector-backed layers consume the per-run role/metadata index. Contextual dependency/allow-only and port-boundary contracts consume the same semantic evidence directly.

Schema-accepted future classification fields are documented explicitly as deferred/no-op where applicable; do not infer support from schema presence alone. See [Semantic classification](semantic-classification.md).

## Reusable source sets

`source_sets` can describe bounded reusable assembly, layer, or project inputs. Supported contract families can fan out over `sources`/`source_sets`, while selected list-shaped fields can union resolved sets. Set expansion never widens beyond the policy's declared analysis universe and fails closed on unknown, mismatched, or unreviewed empty inputs.

## Policy authoring workflow

Before opening a policy change:

```bash
arch-linter-net policy check --policy architecture/arch.yml
arch-linter-net policy context --policy architecture/arch.yml --format json > policy-context.json
arch-linter-net --policy architecture/arch.yml --mode strict
```

For base/current review, compare exported contexts with `policy weakening`. For repository change review, use `change snapshot`, `change report`, and `gate`.

## Sources of truth

When two descriptions disagree, use this precedence:

1. executable CLI/runtime validators and policy schema;
1. `archlinternet.capabilities.json`;
1. mechanically checked public references;
1. handwritten guides and examples.

A public documentation discrepancy is a defect; it should not be resolved by changing runtime behavior merely to preserve stale prose.
