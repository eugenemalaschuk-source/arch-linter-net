# Coverage Contracts

Coverage contracts detect parts of the policy that are not actually exercised against the analyzed codebase: namespaces with no representing layer (`scope: namespace`), or rules whose source/target layer references are dangling or currently match no first-party code (`scope: rule_input`).

Groups:

- `strict_coverage`
- `audit_coverage`

Use coverage contracts when you want architectural namespace discovery, or rule-input health, to stay intentional as the codebase grows. They are especially useful for feature-folder architectures, modular monoliths, template-driven layer layouts, and policies with many hand-authored dependency/layer rules where a typo'd layer name or a rule that quietly stopped matching anything should not pass silently.

## Namespace coverage example

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

## Rule-input coverage example

```yaml
contracts:
  strict:
    - id: cli-must-not-depend-on-testing
      name: cli-must-not-depend-on-testing
      source: cli
      forbidden: [testing]
      reason: CLI must stay independent from test-only helpers.

  strict_coverage:
    - id: rule-input-coverage
      name: rule-input-coverage
      scope: rule_input
      contract_ids: [cli-must-not-depend-on-testing]
      exclude:
        - contract_id: some-other-rule
          reason: This rule intentionally targets a layer that has no code yet.
      reason: Flag rules whose source/target layers stop matching any code.
```

A `scope: rule_input` contract resolves each entry in `contract_ids` to its referenced contract's layer-bearing fields (`source`, `forbidden`, `allowed`, `layers`, `protected`, `allowed_importers`, depending on the contract family) and reports:

- `unresolved` — the referenced field names a layer that is not declared under `layers` at all (a typo or a deleted layer);
- `empty-input` — the referenced field names a declared layer whose namespace pattern currently matches zero namespaces in the analyzed codebase, whether it never matched any code or stopped matching after a rename/deletion.

`contract_ids` may reference dependency, layer, allow_only, cycle, method_body, independence, protected, or external contracts — the families whose layer-bearing fields are plain `layers` keys. Referencing an unknown contract ID, an asmdef/acyclic_sibling/layer_template contract (whose fields are not plain layer-name references), or a coverage contract, is rejected at load time.

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

## Coverage summary

Alongside the raw findings, `validate` reports a deterministic coverage summary for every coverage contract that ran, independent of `analysis.coverage` severity (it is reported even when `coverage: warn` or `coverage: off`). The summary buckets each in-scope item into exactly one of five counts:

| Count | `scope: namespace` meaning | `scope: rule_input` meaning |
|-------|------------------------------|------------------------------|
| `covered` | namespace matched a declared layer, namespace-glob layer, or expanded layer template | referenced layer exists and currently matches code |
| `excluded` | namespace matched an `exclude` rule | referenced contract ID matched an `exclude` rule |
| `uncovered` | namespace matched none of the above (`"uncovered namespace"` finding) | always `0` — this scope reports `stale`/`unknown` instead |
| `stale` | always `0` — this scope reports `uncovered` instead | referenced layer exists but currently matches zero namespaces (`"empty-input"` finding) |
| `unknown` | always `0` | referenced field names a layer that isn't declared at all (`"unresolved"` finding) |

Each excluded item is reported with its `reason` text from the contract's `exclude` entry. Each uncovered/stale/unknown item is reported with evidence: a representative type for namespace coverage, or the dangling/empty layer name for rule-input coverage.

In human output, the summary appears in a `Coverage summary:` section, one line per contract, after `Coverage findings:`:

```
Coverage summary:
- [validation-namespace-coverage] [validation-namespace-coverage] scope: namespace covered=0 excluded=0 uncovered=1 stale=0 unknown=0
    uncovered: ArchLinterNet.Core.Validation (ArchLinterNet.Core.Validation.ArchitectureBaselineService)
```

In JSON output, the summary appears as a top-level `coverage_summary` array, additive to (not nested inside) `coverage_findings`:

```json
{
  "coverage_summary": [
    {
      "contract": "validation-namespace-coverage",
      "contract_id": "validation-namespace-coverage",
      "scope": "namespace",
      "counts": { "covered": 0, "excluded": 0, "uncovered": 1, "stale": 0, "unknown": 0 },
      "excluded_items": [],
      "uncovered_items": [
        { "item": "ArchLinterNet.Core.Validation", "evidence": "ArchLinterNet.Core.Validation.ArchitectureBaselineService" }
      ]
    }
  ]
}
```

`scope: project`, `scope: assembly`, and `scope: dependency_edge` never appear in the summary, since `ArchitectureContractLoader` rejects those scopes at load time (see [Current limits](#current-limits)) — there is no contract instance to summarize for them.

## Exclusion rules

Use `exclude` only for units you intentionally do not want to model yet. For `scope: namespace`, exclude by namespace:

```yaml
exclude:
  - namespace: MyApp.Features.Legacy
    reason: Legacy feature area is being migrated incrementally.
  - namespace: MyApp.Features.*
    namespace_suffix: Generated
    reason: Generated code is excluded from manual architecture coverage.
```

For `scope: rule_input`, exclude by the referenced contract's ID:

```yaml
exclude:
  - contract_id: some-other-rule
    reason: This rule intentionally targets a layer that has no code yet.
```

Rules:

- every exclusion must include a non-empty `reason`;
- namespace coverage exclusions support the same `namespace`/`namespace_suffix` matching model as layers;
- rule-input coverage exclusions must use `contract_id` and suppress both `unresolved` and `empty-input` findings for that referenced contract;
- exclusions are the right place for generated code, temporary migration debt, intentionally unused rules, or known framework-produced namespaces that should not become layers.

## Current limits

Coverage support is intentionally narrow in the current product surface:

- `scope: namespace` and `scope: rule_input` are implemented;
- `scope: project`, `scope: assembly`, and `scope: dependency_edge` are reserved and currently fail validation;
- namespace coverage roots must use `roots[].namespace`;
- discovery-style fields such as `include` and `exclude` are not valid on namespace coverage roots;
- rule-input coverage contracts must use `contract_ids` and must not declare `roots` or `between`.

For the YAML contract shape, see [YAML schema reference](../reference/yaml-schema.md#coverage-contract).
