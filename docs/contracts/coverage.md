# Coverage Contracts

Coverage contracts detect parts of the policy that are not actually exercised against the analyzed codebase: namespaces with no representing layer (`scope: namespace`), rules whose source/target layer references are dangling or currently match no first-party code (`scope: rule_input`), projects or assemblies with no code mapped to any layer (`scope: project`/`scope: assembly`), or observed dependency edges between declared layers that no rule actually governs (`scope: dependency_edge`).

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

| Count | `scope: namespace` | `scope: project`/`scope: assembly` | `scope: dependency_edge` | `scope: rule_input` |
|-------|---------------------|-------------------------------------|---------------------------|------------------------|
| `covered` | namespace matched a declared layer, namespace-glob layer, or expanded layer template | project/assembly has at least one matching type | observed edge's pair is governed by an existing dependency, layer, independence, or expanded-template contract | referenced layer exists and currently matches code |
| `excluded` | namespace matched an `exclude` rule | project/assembly matched an `exclude` rule | observed edge's pair matched an `exclude` rule | referenced contract ID matched an `exclude` rule |
| `uncovered` | namespace matched none of the above (`"uncovered namespace"` finding) | no matching type found (`"uncovered project"`/`"uncovered assembly"` finding) | pair is not governed and not excluded (`"uncovered dependency edge"` finding) | always `0` — this scope reports `stale`/`unknown` instead |
| `stale` | always `0` — this scope reports `uncovered` instead | always `0` | always `0` | referenced layer exists but currently matches zero namespaces (`"empty-input"` finding) |
| `unknown` | always `0` | `scope: project` only — discovered project could not be resolved to a target assembly (`"unresolved project"` finding); always `0` for `scope: assembly` | always `0` | referenced field names a layer that isn't declared at all (`"unresolved"` finding) |

Each excluded item is reported with its `reason` text from the contract's `exclude` entry. Each covered/uncovered/stale/unknown item is reported with evidence — a representative type for namespace/project/assembly coverage, the source/target namespace pair plus a representative source type for dependency-edge coverage, or the dangling/empty layer name for rule-input coverage — and is kept in a bucket-specific list (`covered_items` for every scope; `uncovered_items` for namespace/project/assembly/dependency-edge coverage, `unknown_items` additionally for project coverage, `stale_items`/`unknown_items` for rule-input coverage) rather than a single combined list, since `stale` and `unknown` mean different things and must stay distinguishable by a reviewer or downstream tooling. `covered_items` is the only list naming units found covered by positive evidence — downstream tooling (such as a CI new-code coverage report) should never infer "covered" from a unit's mere absence from the other lists.

In human output, the summary appears in a `Coverage summary:` section, one line per contract, after `Coverage findings:`, with each evidence sub-line explicitly labeled `uncovered:`, `stale:`, or `unknown:`:

```
Coverage summary:
- [validation-namespace-coverage] [validation-namespace-coverage] scope: namespace covered=0 excluded=0 uncovered=1 stale=0 unknown=0
    uncovered: ArchLinterNet.Core.Validation (ArchLinterNet.Core.Validation.ArchitectureBaselineService)
- [rule-input-coverage] [rule-input-coverage] scope: rule_input covered=2 excluded=0 uncovered=0 stale=1 unknown=1
    stale: ghost-rule (ghost)
    unknown: typo-rule (does_not_exist_layer)
```

In JSON output, the summary appears as a top-level `coverage_summary` array, additive to (not nested inside) `coverage_findings`. Every entry always includes all four evidence arrays (`covered_items`, `uncovered_items`, `stale_items`, `unknown_items`); only the ones relevant to the contract's scope are ever non-empty (besides `covered_items`, which is populated whenever a unit is found covered, for every scope) — namespace, project, assembly, and dependency-edge coverage populate `uncovered_items` (project coverage additionally populates `unknown_items` for unresolved projects), rule-input coverage only populates `stale_items`/`unknown_items`:

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
      "unknown_items": [],
      "covered_items": []
    },
    {
      "contract": "rule-input-coverage",
      "contract_id": "rule-input-coverage",
      "scope": "rule_input",
      "counts": { "covered": 2, "excluded": 0, "uncovered": 0, "stale": 1, "unknown": 1 },
      "excluded_items": [],
      "uncovered_items": [],
      "stale_items": [{ "item": "ghost-rule", "evidence": "ghost" }],
      "unknown_items": [{ "item": "typo-rule", "evidence": "does_not_exist_layer" }],
      "covered_items": [
        { "item": "core-validation:core_validation", "evidence": "core_validation" },
        { "item": "core-validation:core_execution", "evidence": "core_execution" }
      ]
    }
  ]
}
```

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

## Dependency-edge coverage

`scope: dependency_edge` answers a narrower question than namespace/project/assembly coverage:
for code that *is* inside a declared layer, is the specific dependency edge between two layers
actually governed by a dependency, layer, independence, allow-only, protected, or layer-template
contract — or does it bypass policy because no contract happens to mention that layer pair?

Each contract declares `between` as a list of declared-layer-name pairs. For every pair, the
system looks at every observed first-party namespace-to-namespace edge whose source namespace
resolves to the pair's first layer and target namespace resolves to the pair's second layer, and
classifies it:

```yaml
analysis:
  coverage: error

contracts:
  strict:
    - id: cli-must-not-depend-on-testing
      name: cli-must-not-depend-on-testing
      source: cli
      forbidden: [testing]
      reason: CLI must stay independent from test-only helpers.

  strict_coverage:
    - id: layer-edge-coverage
      name: layer-edge-coverage
      scope: dependency_edge
      between:
        - [cli, testing]
        - [cli, core]
      exclude:
        - between: [cli, core]
          reason: Already enforced structurally; CLI is allowed to depend on core.
      reason: Every edge between cli and other layers must be governed by a declared rule.
```

A pair is **covered** when any of these already governs it:

- a dependency contract whose `source` equals the pair's first layer and whose `forbidden` list
  contains the second layer;
- a layer contract (`scope: layer`/`strict_layers`/`audit_layers`) whose `layers` chain contains
  both layer names, in either order — the chain's ordering check governs every pair within it;
- an independence contract whose `layers` list contains both layer names — independence is
  bidirectional by definition;
- an allow-only contract whose `source` equals the pair's first layer — an allow-only contract
  governs the *entire* outbound surface of its source layer, so the pair is covered even if the
  second layer is not itself in that contract's `allowed` list;
- a protected contract whose `protected` list contains the pair's second layer — a protected
  contract governs *every* reference into its protected layer, allowed and disallowed alike, so
  the pair is covered even if the first layer is not itself in that contract's
  `allowed_importers` list;
- an expanded [layer template](layer-templates.md) whose container layers match both declared
  layers' namespace patterns.

A pair that matches none of the above, and matches no `exclude` entry, is **uncovered**: every
observed edge for that pair produces an `"uncovered dependency edge"` finding naming the source
namespace, target namespace, and a representative source type. Layer pairs that are not declared
in any `between` list are simply not evaluated — they are out of scope, not a fourth status.

Exclusions match by declared pair, not by individual namespace: `exclude[].between` names the same
`[sourceLayer, targetLayer]` pair as the contract's own `between` entry, plus a required `reason`.
Every observed edge for an excluded pair is suppressed.

## Current limits

Coverage support is intentionally narrow in the current product surface:

- `scope: namespace`, `scope: rule_input`, `scope: project`, `scope: assembly`, and
  `scope: dependency_edge` are implemented;
- namespace coverage roots must use `roots[].namespace`;
- discovery-style fields such as `include` and `exclude` are not valid on namespace coverage roots;
- rule-input coverage contracts must use `contract_ids` and must not declare `roots` or `between`;
- `scope: project`/`scope: assembly` contracts must not declare `roots`, `between`, or `contract_ids`;
- `scope: dependency_edge` contracts must declare `between` and must not declare `roots` or
  `contract_ids`; both layer names in every `between` pair must be declared under `layers`.

For the YAML contract shape, see [YAML schema reference](../reference/yaml-schema.md#coverage-contract).
