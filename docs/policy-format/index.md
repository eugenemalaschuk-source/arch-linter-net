# Policy Format

The architecture policy file usually lives at:

```text
architecture/dependencies.arch.yml
```

The CLI path is configurable with `--policy <path>`.

## Top-level structure

```yaml
version: 1
name: My Architecture Contract

layers: {}
external_dependencies: {}
legacy_runtime_layers: []
analysis: {}
contracts: {}
```

| Section | Purpose |
|---------|---------|
| `version` | Policy schema version. Current value is `1`. |
| `name` | Human-readable policy name. |
| `layers` | Named first-party or external namespace surfaces used by contracts. |
| `external_dependencies` | Named vendor/framework dependency groups. |
| `legacy_runtime_layers` | Optional compatibility namespace groups for runtime-only assemblies. |
| `analysis` | Assembly resolution, source roots, condition sets, and validation behavior. |
| `contracts` | Strict and audit contract families. |

## Minimal policy

```yaml
version: 1
name: Minimal Architecture Contract

layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure

analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
    - MyApp.Infrastructure

contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application must not depend on Infrastructure directly.
```

See [First policy](../getting-started/first-policy.md) for a walkthrough.

## Layers

Layers map short policy names to namespace patterns. They can represent application layers, modules, slices, or external namespace surfaces.

Read [Layers and namespace patterns](layers-and-namespaces.md) for literal prefix matching, constrained `*` globs, `namespace_suffix`, and `external: true`.

## External dependencies

Use `external_dependencies` for vendor/framework leakage checks such as Unity, Entity Framework Core, cloud SDKs, database clients, or payment SDKs.

Read [External dependencies](external-dependencies.md) for the YAML shape and matching rules.

## Analysis configuration

```yaml
analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
  assembly_search_paths: []
  source_roots: []
  condition_sets: {}
  default_condition_set: ''
  unmatched_ignored_violations: error
  policy_consistency: error
  coverage: error
```

`target_assemblies` tells the runner which assemblies to inspect. `assembly_search_paths` and `source_roots` make standalone CLI and method-body scanning reliable in real repositories.

`policy_consistency` controls a separate pass that checks the policy document itself for internal contradictions (duplicate contract IDs, allow/forbid conflicts, independence conflicts, protected-importer conflicts, layer overlaps, unreachable contracts) — independent of code scanning. See [YAML schema reference](../reference/yaml-schema.md#policy_consistency) for details.

`coverage` controls whether declared namespace coverage findings fail validation
(`error`), are reported without failing (`warn`), or are suppressed (`off`).

See [Coverage contracts](../contracts/coverage.md) for authoring guidance, exclusion rules, and current limits.

Read [Condition sets](condition-sets.md) for conditional compilation behavior.

## Contracts

Contracts are split into strict and audit groups. Strict contracts block the run when they fail. Audit contracts are diagnostic and should be used for migration discovery or future-state rules.

```yaml
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_assembly_independence: []
  strict_assembly_dependency: []
  strict_assembly_allow_only: []
  strict_external: []
  strict_external_allow_only: []
  strict_acyclic_siblings: []
  strict_protected: []
  strict_layer_templates: []
  strict_type_placement: []
  strict_coverage: []

  audit: []
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
  audit_assembly_independence: []
  audit_assembly_dependency: []
  audit_assembly_allow_only: []
  audit_external: []
  audit_external_allow_only: []
  audit_acyclic_siblings: []
  audit_protected: []
  audit_layer_templates: []
  audit_type_placement: []
  audit_coverage: []
```

Read [Contracts](../contracts/index.md) for the supported contract families.

## Namespace and rule-input coverage

ArchLinterNet supports coverage contracts for namespace scope and rule-input
scope. Namespace coverage (`scope: namespace`) detects first-party namespaces
under a configured root that are not represented by any declared layer,
namespace glob, expanded layer template, or explicit exclusion. Rule-input
coverage (`scope: rule_input`) detects referenced contracts whose
source/target layer references are dangling or currently match no first-party
code.

```yaml
analysis:
  coverage: error

contracts:
  strict_coverage:
    - id: feature-namespace-coverage
      name: feature-namespace-coverage
      scope: namespace
      roots:
        - namespace: MyApp.Features
      exclude:
        - namespace: MyApp.Features.*
          namespace_suffix: Generated
          reason: Generated code is excluded from manual architecture coverage.
      reason: Every feature namespace must be declared as a layer or explicitly excluded.
```

Coverage contracts also support `ignored_violations`, allowing existing
coverage debt to be baselined the same way ordinary dependency violations are.
See [migration baselines](../guides/migration-baselines.md#coverage-baselines).

Current limits:

- `scope: namespace` and `scope: rule_input` are implemented.
- `project`, `assembly`, and `dependency_edge` coverage scopes remain
  unsupported and fail validation.
- Every `exclude` entry must include a non-empty `reason`.

For user-facing examples and behavior, see [Coverage contracts](../contracts/coverage.md).

## Baselines and ignored violations

Use `ignored_violations` or a generated baseline only to freeze known existing debt. New violations should still be detected.

Read [Migration baselines](../guides/migration-baselines.md) for the full lifecycle.

## Supported capabilities and non-goals

Before adding fields, check [Supported capabilities and non-goals](supported-capabilities.md). ArchLinterNet intentionally does not validate runtime dependency injection behavior, security/authorization correctness, code ownership, or semantic data flow.
