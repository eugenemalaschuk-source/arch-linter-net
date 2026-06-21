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
```

`target_assemblies` tells the runner which assemblies to inspect. `assembly_search_paths` and `source_roots` make standalone CLI and method-body scanning reliable in real repositories.

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
  strict_external: []
  strict_acyclic_siblings: []
  strict_protected: []
  strict_layer_templates: []

  audit: []
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
  audit_external: []
  audit_acyclic_siblings: []
  audit_protected: []
  audit_layer_templates: []
```

Read [Contracts](../contracts/index.md) for the supported contract families.

## Baselines and ignored violations

Use `ignored_violations` or a generated baseline only to freeze known existing debt. New violations should still be detected.

Read [Migration baselines](../guides/migration-baselines.md) for the full lifecycle.

## Supported capabilities and non-goals

Before adding fields, check [Supported capabilities and non-goals](supported-capabilities.md). ArchLinterNet intentionally does not validate runtime dependency injection behavior, security/authorization correctness, code ownership, or semantic data flow.
