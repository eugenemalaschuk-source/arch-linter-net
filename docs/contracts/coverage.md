# Namespace Coverage Contracts

Namespace coverage contracts detect first-party namespaces under a configured root that are not represented by any declared layer, namespace glob layer, expanded layer template, or explicit exclusion.

Groups:

- `strict_coverage`
- `audit_coverage`

Use coverage contracts when you want architectural namespace discovery to stay intentional as the codebase grows. They are especially useful for feature-folder architectures, modular monoliths, and template-driven layer layouts where new namespaces should not appear silently.

## Example

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

## What counts as covered

A namespace under `roots` is considered covered when at least one of these is true:

- it matches a declared layer;
- it matches a declared namespace-glob layer such as `MyApp.Features.*`;
- it is introduced by an expanded [layer template](layer-templates.md);
- it matches an explicit `exclude` rule.

Coverage is evaluated against discovered first-party namespaces that contain loadable types, not against arbitrary strings in the repository.

## Severity behavior

`analysis.coverage` controls whether findings affect the run result:

| Value | Behavior |
|------|----------|
| `error` | Coverage findings fail validation and produce exit code `1`. |
| `warn` | Findings are reported but the run still passes. |
| `off` | Coverage findings are suppressed. |

In human output, findings appear in a separate `Coverage findings:` section. In JSON output, they appear in the top-level `coverage_findings` array alongside `violations`, `cycles`, `unmatched_ignored_violations`, and `policy_consistency_findings`.

## Exclusion rules

Use `exclude` only for namespaces you intentionally do not want to model yet:

```yaml
exclude:
  - namespace: MyApp.Features.Legacy
    reason: Legacy feature area is being migrated incrementally.
  - namespace: MyApp.Features.*
    namespace_suffix: Generated
    reason: Generated code is excluded from manual architecture coverage.
```

Rules:

- every exclusion must include a non-empty `reason`;
- exclusions support the same namespace and `namespace_suffix` matching model as layers;
- exclusions are the right place for generated code, temporary migration debt, or known framework-produced namespaces that should not become layers.

## Current limits

Coverage support is intentionally narrow in the current product surface:

- only `scope: namespace` is implemented;
- `scope: project`, `scope: assembly`, `scope: dependency_edge`, and `scope: rule_input` are reserved and currently fail validation;
- namespace coverage roots must use `roots[].namespace`;
- discovery-style fields such as `include` and `exclude` are not valid on namespace coverage roots.

For the YAML contract shape, see [YAML schema reference](../reference/yaml-schema.md#coverage-contract).
