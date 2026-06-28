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

Each excluded item is reported with its `reason` text from the contract's `exclude` entry. Each uncovered/stale/unknown item is reported with evidence — a representative type for namespace coverage, or the dangling/empty layer name for rule-input coverage — and is kept in a bucket-specific list (`uncovered_items` for namespace coverage, `stale_items`/`unknown_items` for rule-input coverage) rather than a single combined list, since `stale` and `unknown` mean different things and must stay distinguishable by a reviewer or downstream tooling.

In human output, the summary appears in a `Coverage summary:` section, one line per contract, after `Coverage findings:`, with each evidence sub-line explicitly labeled `uncovered:`, `stale:`, or `unknown:`:

```
Coverage summary:
- [validation-namespace-coverage] [validation-namespace-coverage] scope: namespace covered=0 excluded=0 uncovered=1 stale=0 unknown=0
    uncovered: ArchLinterNet.Core.Validation (ArchLinterNet.Core.Validation.ArchitectureBaselineService)
- [rule-input-coverage] [rule-input-coverage] scope: rule_input covered=2 excluded=0 uncovered=0 stale=1 unknown=1
    stale: ghost-rule (ghost)
    unknown: typo-rule (does_not_exist_layer)
```

In JSON output, the summary appears as a top-level `coverage_summary` array, additive to (not nested inside) `coverage_findings`. Every entry always includes all three evidence arrays (`uncovered_items`, `stale_items`, `unknown_items`); only the ones relevant to the contract's scope are ever non-empty — namespace coverage only populates `uncovered_items`, rule-input coverage only populates `stale_items`/`unknown_items`:

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
      ],
      "stale_items": [],
      "unknown_items": []
    },
    {
      "contract": "rule-input-coverage",
      "contract_id": "rule-input-coverage",
      "scope": "rule_input",
      "counts": { "covered": 2, "excluded": 0, "uncovered": 0, "stale": 1, "unknown": 1 },
      "excluded_items": [],
      "uncovered_items": [],
      "stale_items": [{ "item": "ghost-rule", "evidence": "ghost" }],
      "unknown_items": [{ "item": "typo-rule", "evidence": "does_not_exist_layer" }]
    }
  ]
}
```

`scope: project`, `scope: assembly`, and `scope: dependency_edge` never appear in the summary, since `ArchitectureContractLoader` rejects those scopes at load time (see [Current limits](#current-limits)) — there is no contract instance to summarize for them.

A coverage contract only appears in the summary when it is actually selected to run. If `validate --contract <id>` is used to run only specific contracts and a coverage contract's ID isn't among them, that coverage contract is omitted from `coverage_summary` entirely — it never appears as a zero-count row.

## Baselining coverage debt

Coverage contracts support `ignored_violations`, the same baseline mechanism described in
[migration baselines](../guides/migration-baselines.md#coverage-baselines). This lets you adopt a
coverage gate incrementally on a codebase that already has uncovered namespaces or stale
rule-input references, by accepting the current findings as baseline debt and failing only on new
ones:

```yaml
contracts:
  strict_coverage:
    - id: feature-namespace-coverage
      name: feature-namespace-coverage
      scope: namespace
      roots:
        - namespace: MyApp.Features
      ignored_violations:
        - source_type: MyApp.Features.Legacy
          forbidden_reference: "uncovered namespace"
          reason: "Accepted — tracked in #103"
```

`arch-linter-net baseline generate` and `validate --baseline` work the same way for
`strict_coverage`/`audit_coverage` contracts as they do for ordinary dependency contracts.
Coverage baseline entries only suppress coverage findings — they never affect `strict`/`audit`
dependency violations. A baselined coverage finding that has since been resolved is reported as a
stale baseline entry via `unmatched_ignored_violations`.

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

## Project and assembly coverage

`scope: project` and `scope: assembly` detect first-party `.csproj` projects or resolved
assemblies that have no code matching any declared layer, namespace-glob layer, or expanded
layer-template layer — the same coverage-provider rule namespace coverage uses, just applied to a
whole project/assembly instead of a namespace. Use these scopes when you want to catch a project
or assembly that was added to the solution (or renamed) but never wired into any layer at all,
something namespace coverage cannot see if nobody rooted it.

Neither scope declares `roots`; they classify every unit discovered/resolved for the run.

```yaml
analysis:
  solution: MyApp.slnx
  coverage: error

contracts:
  strict_coverage:
    - id: project-coverage
      name: project-coverage
      scope: project
      exclude:
        - project: samples/Demo/Demo.csproj
          reason: Sample project is intentionally out of architecture scope.
      reason: Every discovered project must be mapped to a layer or explicitly excluded.

    - id: assembly-coverage
      name: assembly-coverage
      scope: assembly
      exclude:
        - assembly: MyApp.TestUtilities
          reason: Test-only helper assembly.
      reason: Every resolved assembly must be mapped to a layer or explicitly excluded.
```

`scope: project` requires `analysis.solution` or `analysis.projects` to be set, since discovered
projects (path, assembly name, target frameworks) are the units it classifies — see
`analysis.solution`/`analysis.projects` in the
[YAML schema reference](../reference/yaml-schema.md) for how those are resolved. `scope: assembly`
has no such requirement: it classifies `ArchitectureAnalysisContext`'s resolved first-party
assemblies, which exist whether they came from explicit `analysis.target_assemblies` or from
discovery.

A discovered project that cannot be resolved to one of the run's target assemblies (filtered out,
missing/stale build output, ambiguous multi-target selection) is classified `unknown`, not
`uncovered` — it produces an `"unresolved project"` finding naming the project path and the
assembly name discovery expected, rather than asserting the project's code is unmapped.

Exclusions use `exclude[].project` (matched against the discovered project's repository-relative
path or its file name, exact match) or `exclude[].assembly` (matched against the assembly's simple
name, ordinal), each with a required `reason`. Unlike namespace coverage, `project`/`assembly`
exclusions do not support glob matching.

`.asmdef` contracts (the Unity assembly-definition checks documented in
[asmdef contracts](asmdef.md)) are a separate mechanism with their own identity model and are not
affected by, or folded into, `scope: project`/`scope: assembly` coverage.

## Current limits

Coverage support is intentionally narrow in the current product surface:

- `scope: namespace`, `scope: rule_input`, `scope: project`, and `scope: assembly` are implemented;
- `scope: dependency_edge` is reserved and currently fails validation;
- namespace coverage roots must use `roots[].namespace`;
- discovery-style fields such as `include` and `exclude` are not valid on namespace coverage roots;
- rule-input coverage contracts must use `contract_ids` and must not declare `roots` or `between`;
- `scope: project`/`scope: assembly` contracts must not declare `roots`, `between`, or `contract_ids`.

For the YAML contract shape, see [YAML schema reference](../reference/yaml-schema.md#coverage-contract).
